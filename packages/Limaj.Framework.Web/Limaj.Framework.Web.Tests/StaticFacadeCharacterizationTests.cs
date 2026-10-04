using System.Net;
using Limaj.Framework.Core;
using Limaj.Framework.Core.Errors;
using Limaj.Framework.Web.Http;
using Limaj.Framework.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// Characterization of the static facades' v2 output (ResultExtensions.ToHttpResult,
/// RequestRunner.RunAsync / ExceptionExtensions.ToHttpResult), executed on a real
/// HttpContext without AddProblemDetails: concrete IResult type, status, Content-Type and
/// title/detail. Locked before the web-error-extensibility refactor (Phase 1); only the
/// DA-005 security fixes are allowed to change it, and those cases say so explicitly
/// ("DA-005:" below: previous rule in the comment, new rule in the assertion).
/// </summary>
public class StaticFacadeCharacterizationTests
{
    private const string ProblemJson = "application/problem+json";
    private const string PlainJson = "application/json; charset=utf-8";

    public static readonly TheoryData<ErrorType, Type, int, string> ErrorTypeCharacterization = new()
    {
        { ErrorType.Validation, typeof(ProblemHttpResult), StatusCodes.Status400BadRequest, ProblemJson },
        { ErrorType.NotFound, typeof(NotFound<ProblemDetails>), StatusCodes.Status404NotFound, PlainJson },
        { ErrorType.Conflict, typeof(Conflict<ProblemDetails>), StatusCodes.Status409Conflict, PlainJson },
        { ErrorType.Forbidden, typeof(ProblemHttpResult), StatusCodes.Status403Forbidden, ProblemJson },
        { ErrorType.Unauthorized, typeof(ProblemHttpResult), StatusCodes.Status401Unauthorized, ProblemJson },
        { ErrorType.Unexpected, typeof(ProblemHttpResult), StatusCodes.Status500InternalServerError, ProblemJson },
        { ErrorType.TooManyRequests, typeof(ProblemHttpResult), StatusCodes.Status429TooManyRequests, ProblemJson }
    };

    [Theory]
    [MemberData(nameof(ErrorTypeCharacterization))]
    public async Task ResultOfT_ToHttpResult_KeepsV2ShapePerErrorType(
        ErrorType errorType,
        Type expectedResultType,
        int expectedStatusCode,
        string expectedContentType)
    {
        var httpResult = Result<object>.Fail(BuildError(errorType)).ToHttpResult(value => Results.Ok(value));

        Assert.IsType(expectedResultType, httpResult);
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(expectedContentType, response.ContentType);
        AssertV2Body(errorType, response);
    }

    [Theory]
    [MemberData(nameof(ErrorTypeCharacterization))]
    public async Task Result_ToHttpResult_KeepsV2ShapePerErrorType(
        ErrorType errorType,
        Type expectedResultType,
        int expectedStatusCode,
        string expectedContentType)
    {
        var httpResult = Result.Fail(BuildError(errorType)).ToHttpResult(Results.NoContent);

        Assert.IsType(expectedResultType, httpResult);
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(expectedContentType, response.ContentType);
        AssertV2Body(errorType, response);
    }

    [Fact]
    public async Task ToHttpResult_CustomHttpStatusCode_IsProblemWithTitleMessageAndDetailCode()
    {
#pragma warning disable CS0618 // Characterizes the deprecated HttpStatusCode escape hatch (DA-010).
        var error = new Error("plan_limit_exceeded", "Plan limit exceeded.", ErrorType.Unexpected, HttpStatusCode: HttpStatusCode.PaymentRequired);
#pragma warning restore CS0618

        var httpResult = Result<object>.Fail(error).ToHttpResult(value => Results.Ok(value));

        Assert.IsType<ProblemHttpResult>(httpResult);
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(StatusCodes.Status402PaymentRequired, response.StatusCode);
        Assert.Equal(ProblemJson, response.ContentType);
        Assert.Equal("Plan limit exceeded.", response.GetString("title"));
        Assert.Equal("plan_limit_exceeded", response.GetString("detail"));
    }

    [Fact]
    public async Task RunAsync_DomainValidationException_IsValidationProblemWithErrors()
    {
        var errors = new Dictionary<string, string[]> { ["email"] = ["required"] };

        var httpResult = await RequestRunner.RunAsync(
            () => throw new DomainValidationException(errors, "Invalid input."),
            NullLogger.Instance,
            "TestOperation");

        Assert.IsType<ProblemHttpResult>(httpResult);
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.ContentType);
        Assert.Equal("Invalid input.", response.GetString("title"));
        Assert.Equal("required", response.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task RunAsync_NotFoundException_IsNotFoundWithTitleMessageAndDetailResource()
    {
        var httpResult = await RequestRunner.RunAsync(
            () => throw new NotFoundException("User", "u-1"),
            NullLogger.Instance,
            "TestOperation");

        Assert.IsType<NotFound<ProblemDetails>>(httpResult);
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(PlainJson, response.ContentType);
        Assert.Equal("User was not found.", response.GetString("title"));
        Assert.Equal("User", response.GetString("detail"));
    }

    [Fact]
    public async Task RunAsync_ConflictException_IsConflictWithTitleMessageAndDetailConflict()
    {
        var httpResult = await RequestRunner.RunAsync(
            () => throw new ConflictException("Already exists."),
            NullLogger.Instance,
            "TestOperation");

        Assert.IsType<Conflict<ProblemDetails>>(httpResult);
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Equal(PlainJson, response.ContentType);
        Assert.Equal("Already exists.", response.GetString("title"));
        Assert.Equal("conflict", response.GetString("detail"));
    }

    [Fact]
    public async Task RunAsync_UnknownException_OutsideDevelopment_IsProblem500WithGenericTitle()
    {
        var httpResult = await RequestRunner.RunAsync(
            () => throw new InvalidOperationException("boom, internal detail"),
            NullLogger.Instance,
            "TestOperation");

        Assert.IsType<ProblemHttpResult>(httpResult);
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.Equal(ProblemJson, response.ContentType);
        Assert.Equal("An unexpected error occurred.", response.GetString("title"));
        Assert.DoesNotContain("boom", response.RawBody);
        // DA-005: detail was ex.Source (the throwing assembly's name); it is now a fixed code.
        Assert.Equal("unexpected_error", response.GetString("detail"));
        Assert.DoesNotContain(typeof(StaticFacadeCharacterizationTests).Assembly.GetName().Name!, response.RawBody);
    }

    [Fact]
    public async Task ToHttpResult_ValidationWithCustomHttpStatusCode_KeepsErrors()
    {
#pragma warning disable CS0618 // Characterizes the deprecated HttpStatusCode escape hatch (DA-010).
        var error = new Error(
            "attachment_too_large",
            "The attachment is too large.",
            ErrorType.Validation,
            new Dictionary<string, string[]> { ["attachment"] = ["must be at most 10 MB"] },
            HttpStatusCode.RequestEntityTooLarge);
#pragma warning restore CS0618

        var response = await HttpResultExecutor.ExecuteAsync(Result.Fail(error).ToHttpResult(Results.NoContent));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, response.StatusCode);
        // DA-005: errors used to be dropped when HttpStatusCode was set; they are now kept,
        // with title/detail as on the other custom-status responses.
        Assert.Equal("must be at most 10 MB", response.GetProperty("errors").GetProperty("attachment")[0].GetString());
        Assert.Equal("The attachment is too large.", response.GetString("title"));
        Assert.Equal("attachment_too_large", response.GetString("detail"));
    }

    private static void AssertV2Body(ErrorType errorType, ExecutedHttpResult response)
    {
        // DA-005: an Unexpected result used to send its own message as title in every
        // environment; outside Development (the test process) it now sends the generic one.
        var expectedTitle = errorType == ErrorType.Unexpected ? "An unexpected error occurred." : "message";
        Assert.Equal(expectedTitle, response.GetString("title"));
        if (errorType == ErrorType.Validation)
        {
            // V2 ValidationProblem: no detail, no code anywhere in the body.
            Assert.Null(response.GetString("detail"));
            Assert.Equal("required", response.GetProperty("errors").GetProperty("field")[0].GetString());
        }
        else
        {
            Assert.Equal("code", response.GetString("detail"));
        }

        Assert.False(response.HasProperty("code"));
    }

    private static Error BuildError(ErrorType errorType) =>
        errorType == ErrorType.Validation
            ? new Error("code", "message", errorType, new Dictionary<string, string[]> { ["field"] = ["required"] })
            : new Error("code", "message", errorType);
}
