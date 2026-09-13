using System.Net;
using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Abstractions.Errors;
using Limaj.Framework.Web.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// Covers RequestRunner.RunAsync + ExceptionExtensions.ToHttpResult: the 4 branches of the
/// standard bridge (DomainValidationException, NotFoundException, ConflictException, default)
/// plus the DA-003 IExceptionToErrorMapper extension point for the default branch.
/// </summary>
public class ExceptionBridgeTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;

    [Fact]
    public async Task RunAsync_HandlerSucceeds_ReturnsHandlerResultUnchanged()
    {
        var result = await RequestRunner.RunAsync(() => Task.FromResult(Results.Ok("value")), Logger, "TestOperation");

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_DomainValidationException_MapsToBadRequest()
    {
        var errors = new Dictionary<string, string[]> { ["field"] = ["required"] };

        var result = await RequestRunner.RunAsync(
            () => throw new DomainValidationException(errors),
            Logger,
            "TestOperation");

        Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_NotFoundException_MapsToNotFound()
    {
        var result = await RequestRunner.RunAsync(
            () => throw new NotFoundException("User", "u-1"),
            Logger,
            "TestOperation");

        Assert.Equal(StatusCodes.Status404NotFound, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_ConflictException_MapsToConflict()
    {
        var result = await RequestRunner.RunAsync(
            () => throw new ConflictException("Already exists."),
            Logger,
            "TestOperation");

        Assert.Equal(StatusCodes.Status409Conflict, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_UnknownException_WithoutMapper_MapsToUnexpected500()
    {
        var result = await RequestRunner.RunAsync(
            () => throw new InvalidOperationException("boom"),
            Logger,
            "TestOperation");

        Assert.Equal(StatusCodes.Status500InternalServerError, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_UnknownException_MapperRecognizesIt_UsesMappedErrorStatusCode()
    {
        var mapper = new Mock<IExceptionToErrorMapper>();
        mapper.Setup(m => m.Map(It.IsAny<InvalidOperationException>()))
            .Returns(new Error("plan_limit_exceeded", "Plan limit exceeded.", ErrorType.Unexpected, HttpStatusCode: HttpStatusCode.PaymentRequired));

        var result = await RequestRunner.RunAsync(
            () => throw new InvalidOperationException("boom"),
            Logger,
            "TestOperation",
            mapper.Object);

        Assert.Equal(StatusCodes.Status402PaymentRequired, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_UnknownException_MapperDoesNotRecognizeIt_FallsBackToUnexpected500()
    {
        var mapper = new Mock<IExceptionToErrorMapper>();
        mapper.Setup(m => m.Map(It.IsAny<Exception>())).Returns((Error?)null);

        var result = await RequestRunner.RunAsync(
            () => throw new InvalidOperationException("boom"),
            Logger,
            "TestOperation",
            mapper.Object);

        Assert.Equal(StatusCodes.Status500InternalServerError, GetStatusCode(result));
    }

    [Fact]
    public void ToHttpResult_UnknownException_DevelopmentEnvironment_ReturnsExceptionMessageInBody()
    {
        var ex = new InvalidOperationException("boom, internal EF Core detail");

        var result = ex.ToHttpResult(Logger, "TestOperation", isDevelopmentEnvironment: true);

        var problemDetails = Assert.IsType<ProblemHttpResult>(result).ProblemDetails;
        Assert.Equal(ex.Message, problemDetails.Title);
    }

    [Fact]
    public void ToHttpResult_UnknownException_NonDevelopmentEnvironment_ReturnsGenericMessageInBody()
    {
        var ex = new InvalidOperationException("boom, internal EF Core detail");

        var result = ex.ToHttpResult(Logger, "TestOperation", isDevelopmentEnvironment: false);

        var problemDetails = Assert.IsType<ProblemHttpResult>(result).ProblemDetails;
        Assert.Equal("An unexpected error occurred.", problemDetails.Title);
        Assert.NotEqual(ex.Message, problemDetails.Title);
    }

    private static int GetStatusCode(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode
            ?? throw new InvalidOperationException("Result did not carry a status code.");
}
