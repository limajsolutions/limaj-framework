using Limaj.Framework.Abstractions.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Limaj.Framework.Web.Http;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value!);
        }

        return MapError(result.Error!);
    }

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess();
        }

        return MapError(result.Error!);
    }

    private static IResult MapError(Error error)
    {
        // An explicit HttpStatusCode (set by a host's IExceptionToErrorMapper) always wins over
        // the ErrorType -> status mapping below, so a product can respond with a status the
        // closed 7-value ErrorType enum doesn't cover without a framework change.
        if (error.HttpStatusCode is { } statusCode)
        {
            return Results.Problem(
                title: error.Message,
                detail: error.Code,
                statusCode: (int)statusCode);
        }

        return MapByErrorType(error);
    }

    private static IResult MapByErrorType(Error error) => error.Type switch
    {
        ErrorType.Validation => Results.ValidationProblem(
            errors: error.Details ?? new Dictionary<string, string[]>(),
            title: error.Message),

        ErrorType.NotFound => Results.NotFound(new ProblemDetails
        {
            Title = error.Message,
            Detail = error.Code,
            Status = StatusCodes.Status404NotFound
        }),

        ErrorType.Conflict => Results.Conflict(new ProblemDetails
        {
            Title = error.Message,
            Detail = error.Code,
            Status = StatusCodes.Status409Conflict
        }),

        ErrorType.Forbidden => Results.Problem(
            title: error.Message,
            detail: error.Code,
            statusCode: StatusCodes.Status403Forbidden),

        ErrorType.Unauthorized => Results.Problem(
            title: error.Message,
            detail: error.Code,
            statusCode: StatusCodes.Status401Unauthorized),

        ErrorType.TooManyRequests => Results.Problem(
            title: error.Message,
            detail: error.Code,
            statusCode: StatusCodes.Status429TooManyRequests),

        _ => Results.Problem(
            title: error.Message,
            detail: error.Code,
            statusCode: StatusCodes.Status500InternalServerError)
    };
}
