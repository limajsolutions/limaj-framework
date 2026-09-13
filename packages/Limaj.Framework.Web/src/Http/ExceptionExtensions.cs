using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Abstractions.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Web.Http;

public static class ExceptionExtensions
{
    /// <summary>
    /// Maps known framework exceptions to structured HTTP responses. Exceptions not covered by
    /// the standard bridge are offered to <paramref name="exceptionToErrorMapper"/> (if the host
    /// registered one) before falling back to a generic Unexpected/500 response.
    /// </summary>
    public static IResult ToHttpResult(
        this Exception ex,
        ILogger logger,
        string operationName,
        IExceptionToErrorMapper? exceptionToErrorMapper = null,
        bool? isDevelopmentEnvironment = null)
    {
        switch (ex)
        {
            case DomainValidationException validationException:
                logger.LogWarning("Validation error in {Operation}: {Message}", operationName, ex.Message);
                return Result<object>
                    .Validation(new Dictionary<string, string[]>(validationException.Errors), "validation_failed", ex.Message)
                    .ToHttpResult(_ => Results.BadRequest());

            case NotFoundException notFoundException:
                logger.LogWarning(
                    "Not found in {Operation}: {Resource} ({Message})",
                    operationName,
                    notFoundException.Resource,
                    ex.Message);
                return Result<object>
                    .NotFound(notFoundException.Resource, ex.Message)
                    .ToHttpResult(_ => Results.NotFound());

            case ConflictException:
                logger.LogWarning("Conflict in {Operation}: {Message}", operationName, ex.Message);
                return Result<object>
                    .Conflict("conflict", ex.Message)
                    .ToHttpResult(_ => Results.Conflict());

            default:
                var mappedError = exceptionToErrorMapper?.Map(ex);
                if (mappedError is not null)
                {
                    logger.LogWarning(ex, "Mapped error in {Operation}: {Code}", operationName, mappedError.Code);
                    return Result<object>.Fail(mappedError).ToHttpResult(_ => Results.Problem());
                }

                logger.LogError(ex, "Unhandled error in {Operation}", operationName);
                // DA-004: ex.Message is logged in full above, but never returned to the client
                // outside Development — it can carry EF Core/third-party internals the client
                // has no business seeing.
                var isDevelopment = isDevelopmentEnvironment ?? IsAspNetCoreDevelopmentEnvironment();
                var clientMessage = isDevelopment ? ex.Message : "An unexpected error occurred.";
                return Result<object>
                    .Unexpected(ex.Source ?? "unknown", clientMessage)
                    .ToHttpResult(_ => Results.Problem());
        }
    }

    private static bool IsAspNetCoreDevelopmentEnvironment() =>
        string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);
}
