using Limaj.Framework.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// The framework's default <see cref="IErrorHttpMapper"/> (DA-002): ErrorType -> status, in the
/// configured <see cref="LimajProblemDetailsFormat"/>. Public and unsealed, with one virtual
/// method per <see cref="ErrorType"/> plus <see cref="MapWithStatusCode"/>, so a product
/// inherits or decorates it instead of rewriting it.
/// </summary>
public class DefaultErrorHttpMapper : IErrorHttpMapper
{
    /// <summary>The message a client gets instead of an unexpected (5xx) error's own message.</summary>
    public const string GenericUnexpectedMessage = "An unexpected error occurred.";

    /// <summary>The mapping the static facades delegate to: default options, i.e. V2.</summary>
    internal static readonly DefaultErrorHttpMapper StaticFacadeDefault =
        new(new LimajHttpErrorOptions(), AspNetCoreEnvironment.IsDevelopment);

    private static readonly Dictionary<string, string[]> NoValidationErrors = new();

    private readonly Func<bool> _isDevelopmentEnvironment;

    public DefaultErrorHttpMapper(IOptions<LimajHttpErrorOptions> options)
        : this(options.Value, AspNetCoreEnvironment.IsDevelopment)
    {
    }

    internal DefaultErrorHttpMapper(LimajHttpErrorOptions options, Func<bool> isDevelopmentEnvironment)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _isDevelopmentEnvironment = isDevelopmentEnvironment;
    }

    protected LimajHttpErrorOptions Options { get; }

    private bool IsV3 => Options.Format == LimajProblemDetailsFormat.V3;

    public virtual IResult Map(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

#pragma warning disable CS0618 // Error.HttpStatusCode is deprecated (DA-010) but still honored in 3.x.
        if (error.HttpStatusCode is { } explicitStatusCode)
        {
            return MapWithStatusCode(error, (int)explicitStatusCode);
        }
#pragma warning restore CS0618

        return error.Type switch
        {
            ErrorType.Validation => MapValidation(error),
            ErrorType.NotFound => MapNotFound(error),
            ErrorType.Conflict => MapConflict(error),
            ErrorType.Forbidden => MapForbidden(error),
            ErrorType.Unauthorized => MapUnauthorized(error),
            ErrorType.TooManyRequests => MapTooManyRequests(error),
            _ => MapUnexpected(error)
        };
    }

    public virtual IResult MapValidation(Error error) =>
        CreateValidationProblem(error, StatusCodes.Status400BadRequest, codeInV2Detail: false);

    public virtual IResult MapNotFound(Error error) =>
        IsV3
            ? CreateProblem(error, StatusCodes.Status404NotFound)
            : WithRetryAfter(error, Results.NotFound(CreateV2ProblemDetails(error, StatusCodes.Status404NotFound)));

    public virtual IResult MapConflict(Error error) =>
        IsV3
            ? CreateProblem(error, StatusCodes.Status409Conflict)
            : WithRetryAfter(error, Results.Conflict(CreateV2ProblemDetails(error, StatusCodes.Status409Conflict)));

    public virtual IResult MapForbidden(Error error) => CreateProblem(error, StatusCodes.Status403Forbidden);

    public virtual IResult MapUnauthorized(Error error) => CreateProblem(error, StatusCodes.Status401Unauthorized);

    public virtual IResult MapTooManyRequests(Error error) => CreateProblem(error, StatusCodes.Status429TooManyRequests);

    public virtual IResult MapUnexpected(Error error) => CreateProblem(error, StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Writes <paramref name="error"/> with an explicit status, in the configured format — the
    /// supported path for statuses outside the ErrorType mapping (e.g. 422 keyed on
    /// <c>Error.Code</c>). A Validation error keeps its <c>errors</c>.
    /// </summary>
    public virtual IResult MapWithStatusCode(Error error, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Type == ErrorType.Validation
            ? CreateValidationProblem(error, statusCode, codeInV2Detail: true)
            : CreateProblem(error, statusCode);
    }

    /// <summary>The status the default mapping gives <paramref name="error"/>.</summary>
    internal static int ResolveStatusCode(Error error)
    {
#pragma warning disable CS0618 // Error.HttpStatusCode is deprecated (DA-010) but still honored in 3.x.
        if (error.HttpStatusCode is { } explicitStatusCode)
        {
            return (int)explicitStatusCode;
        }
#pragma warning restore CS0618

        return error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    internal static long? GetRetryAfterSeconds(Error error) =>
        error.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero
            ? (long)Math.Ceiling(retryAfter.TotalSeconds)
            : null;

    private IResult CreateProblem(Error error, int statusCode)
    {
        var clientMessage = GetClientMessage(error, statusCode);
        var extensions = CreateExtensions(error, statusCode, includeDetails: true);

        var problem = IsV3
            ? Results.Problem(
                detail: clientMessage,
                statusCode: statusCode,
                title: GetTitle(statusCode),
                extensions: extensions)
            : Results.Problem(
                title: clientMessage,
                detail: error.Code,
                statusCode: statusCode,
                extensions: extensions);

        return WithRetryAfter(error, problem);
    }

    private IResult CreateValidationProblem(Error error, int statusCode, bool codeInV2Detail)
    {
        var validationErrors = error.Details ?? NoValidationErrors;

        // Validation errors are public by design: they are always sent, never gated by the
        // DA-008 opt-in (that one covers Details on the other error types).
        var problem = IsV3
            ? Results.ValidationProblem(
                validationErrors,
                detail: error.Message,
                title: GetTitle(statusCode),
                statusCode: statusCode,
                extensions: CreateExtensions(error, statusCode, includeDetails: false))
            : Results.ValidationProblem(
                validationErrors,
                detail: codeInV2Detail ? error.Code : null,
                title: error.Message,
                statusCode: statusCode);

        return WithRetryAfter(error, problem);
    }

    private ProblemDetails CreateV2ProblemDetails(Error error, int statusCode)
    {
        var problemDetails = new ProblemDetails
        {
            Title = GetClientMessage(error, statusCode),
            Detail = error.Code,
            Status = statusCode
        };

        if (CreateExtensions(error, statusCode, includeDetails: true) is { } extensions)
        {
            foreach (var extension in extensions)
            {
                problemDetails.Extensions[extension.Key] = extension.Value;
            }
        }

        return problemDetails;
    }

    private Dictionary<string, object?>? CreateExtensions(Error error, int statusCode, bool includeDetails)
    {
        var extensions = new Dictionary<string, object?>();

        if (IsV3)
        {
            extensions["code"] = error.Code;
            if (GetRetryAfterSeconds(error) is { } retryAfterSeconds)
            {
                extensions["retryAfter"] = retryAfterSeconds;
            }
        }

        // DA-008: Details outside Validation only on opt-in, and never on a 5xx.
        if (includeDetails
            && Options.IncludeDetailsOutsideValidation
            && statusCode < StatusCodes.Status500InternalServerError
            && error.Details is { Count: > 0 } details)
        {
            extensions["details"] = details;
        }

        return extensions.Count == 0 ? null : extensions;
    }

    /// <summary>
    /// DA-005: an Unexpected error that ends up as a 5xx sends the generic message unless the
    /// options expose it (IncludeExceptionDetails, or the temporary 3.x opt-out).
    /// </summary>
    private string GetClientMessage(Error error, int statusCode)
    {
        var isUnexpectedServerError =
            error.Type == ErrorType.Unexpected && statusCode >= StatusCodes.Status500InternalServerError;

        return isUnexpectedServerError && !ExposesUnexpectedMessages() ? GenericUnexpectedMessage : error.Message;
    }

    private bool ExposesUnexpectedMessages()
    {
#pragma warning disable CS0618 // Temporary DA-005 opt-out, removed in 4.0.0.
        if (Options.ExposeUnexpectedResultMessage)
        {
            return true;
        }
#pragma warning restore CS0618

        return Options.ResolveIncludeExceptionDetails(_isDevelopmentEnvironment);
    }

    private static string? GetTitle(int statusCode)
    {
        var reasonPhrase = ReasonPhrases.GetReasonPhrase(statusCode);
        return reasonPhrase.Length == 0 ? null : reasonPhrase;
    }

    private static IResult WithRetryAfter(Error error, IResult result) =>
        GetRetryAfterSeconds(error) is { } retryAfterSeconds
            ? new RetryAfterHttpResult(result, retryAfterSeconds)
            : result;
}
