using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Abstractions.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Functions.Http;

public static class ExceptionExtensions
{
    /// <summary>
    /// Maps known framework exceptions to structured HTTP responses.
    /// </summary>
    public static IResult ToHttpResult(this Exception ex, ILogger logger, string functionName)
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
                logger.LogError(ex, "Unhandled error in {Function}", functionName);
                return Result<object>
                    .Unexpected(ex.Source ?? "unknown", ex.Message)
                    .ToHttpResult(_ => Results.Problem());
        }
    }
}
