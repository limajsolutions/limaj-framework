using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Abstractions.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Functions.Http;

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
        string functionName,
        IExceptionToErrorMapper? exceptionToErrorMapper = null)
    {
        switch (ex)
        {
            case DomainValidationException validationException:
                logger.LogWarning("Validation error in {Function}: {Message}", functionName, ex.Message);
                return Result<object>
                    .Validation(new Dictionary<string, string[]>(validationException.Errors), "validation_failed", ex.Message)
                    .ToHttpResult(_ => Results.BadRequest());

            case NotFoundException notFoundException:
                logger.LogWarning(
                    "Not found in {Function}: {Resource} ({Message})",
                    functionName,
                    notFoundException.Resource,
                    ex.Message);
                return Result<object>
                    .NotFound(notFoundException.Resource, ex.Message)
                    .ToHttpResult(_ => Results.NotFound());

            case ConflictException:
                logger.LogWarning("Conflict in {Function}: {Message}", functionName, ex.Message);
                return Result<object>
                    .Conflict("conflict", ex.Message)
                    .ToHttpResult(_ => Results.Conflict());

            default:
                var mappedError = exceptionToErrorMapper?.Map(ex);
                if (mappedError is not null)
                {
                    logger.LogWarning(ex, "Mapped error in {Function}: {Code}", functionName, mappedError.Code);
                    return Result<object>.Fail(mappedError).ToHttpResult(_ => Results.Problem());
                }

                logger.LogError(ex, "Unhandled error in {Function}", functionName);
                return Result<object>
                    .Unexpected(ex.Source ?? "unknown", ex.Message)
                    .ToHttpResult(_ => Results.Problem());
        }
    }
}
