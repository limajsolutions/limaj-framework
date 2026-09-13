using System.Net;
using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Web.Http;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Limaj.Framework.Web.Tests;

public class ResultExtensionsTests
{
    public static readonly TheoryData<ErrorType, int> ErrorTypeToStatusCode = new()
    {
        { ErrorType.Validation, StatusCodes.Status400BadRequest },
        { ErrorType.NotFound, StatusCodes.Status404NotFound },
        { ErrorType.Conflict, StatusCodes.Status409Conflict },
        { ErrorType.Forbidden, StatusCodes.Status403Forbidden },
        { ErrorType.Unauthorized, StatusCodes.Status401Unauthorized },
        { ErrorType.Unexpected, StatusCodes.Status500InternalServerError },
        { ErrorType.TooManyRequests, StatusCodes.Status429TooManyRequests }
    };

    [Theory]
    [MemberData(nameof(ErrorTypeToStatusCode))]
    public void ResultOfT_ToHttpResult_MapsEachErrorTypeToExpectedStatusCode(ErrorType errorType, int expectedStatusCode)
    {
        var error = BuildError(errorType);
        var result = Result<object>.Fail(error);

        var httpResult = result.ToHttpResult(value => Results.Ok(value));

        Assert.Equal(expectedStatusCode, GetStatusCode(httpResult));
    }

    [Theory]
    [MemberData(nameof(ErrorTypeToStatusCode))]
    public void Result_ToHttpResult_MapsEachErrorTypeToExpectedStatusCode(ErrorType errorType, int expectedStatusCode)
    {
        var error = BuildError(errorType);
        var result = Result.Fail(error);

        var httpResult = result.ToHttpResult(Results.NoContent);

        Assert.Equal(expectedStatusCode, GetStatusCode(httpResult));
    }

    [Fact]
    public void ResultOfT_ToHttpResult_OnSuccess_InvokesCallbackWithValue()
    {
        var result = Result<int>.Ok(42);

        var httpResult = result.ToHttpResult(value => Results.Ok(value));

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(httpResult));
    }

    [Fact]
    public void Result_ToHttpResult_OnSuccess_InvokesCallback()
    {
        var result = Result.Ok();

        var httpResult = result.ToHttpResult(Results.NoContent);

        Assert.Equal(StatusCodes.Status204NoContent, GetStatusCode(httpResult));
    }

    [Fact]
    public void ToHttpResult_ExplicitHttpStatusCode_OverridesErrorTypeMapping()
    {
        // DA-003: a host's IExceptionToErrorMapper can set Error.HttpStatusCode to respond with a
        // status the closed 7-value ErrorType enum doesn't cover.
        var error = new Error("plan_limit_exceeded", "Plan limit exceeded.", ErrorType.Unexpected, HttpStatusCode: HttpStatusCode.PaymentRequired);
        var result = Result<object>.Fail(error);

        var httpResult = result.ToHttpResult(value => Results.Ok(value));

        Assert.Equal(StatusCodes.Status402PaymentRequired, GetStatusCode(httpResult));
    }

    private static Error BuildError(ErrorType errorType) =>
        errorType == ErrorType.Validation
            ? new Error("code", "message", errorType, new Dictionary<string, string[]> { ["field"] = ["required"] })
            : new Error("code", "message", errorType);

    private static int GetStatusCode(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode
            ?? throw new InvalidOperationException("Result did not carry a status code.");
}
