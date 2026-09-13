using System.Net;
using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Abstractions.Errors;
using Limaj.Framework.Functions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Limaj.Framework.Functions.Tests;

/// <summary>
/// Covers FunctionRunner.RunAsync + ExceptionExtensions.ToHttpResult: the 4 branches of the
/// standard bridge (DomainValidationException, NotFoundException, ConflictException, default)
/// plus the DA-003 IExceptionToErrorMapper extension point for the default branch.
/// </summary>
public class ExceptionBridgeTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;

    [Fact]
    public async Task RunAsync_HandlerSucceeds_ReturnsHandlerResultUnchanged()
    {
        var result = await FunctionRunner.RunAsync(() => Task.FromResult(Results.Ok("value")), Logger, "TestFunction");

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_DomainValidationException_MapsToBadRequest()
    {
        var errors = new Dictionary<string, string[]> { ["field"] = ["required"] };

        var result = await FunctionRunner.RunAsync(
            () => throw new DomainValidationException(errors),
            Logger,
            "TestFunction");

        Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_NotFoundException_MapsToNotFound()
    {
        var result = await FunctionRunner.RunAsync(
            () => throw new NotFoundException("User", "u-1"),
            Logger,
            "TestFunction");

        Assert.Equal(StatusCodes.Status404NotFound, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_ConflictException_MapsToConflict()
    {
        var result = await FunctionRunner.RunAsync(
            () => throw new ConflictException("Already exists."),
            Logger,
            "TestFunction");

        Assert.Equal(StatusCodes.Status409Conflict, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_UnknownException_WithoutMapper_MapsToUnexpected500()
    {
        var result = await FunctionRunner.RunAsync(
            () => throw new InvalidOperationException("boom"),
            Logger,
            "TestFunction");

        Assert.Equal(StatusCodes.Status500InternalServerError, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_UnknownException_MapperRecognizesIt_UsesMappedErrorStatusCode()
    {
        var mapper = new Mock<IExceptionToErrorMapper>();
        mapper.Setup(m => m.Map(It.IsAny<InvalidOperationException>()))
            .Returns(new Error("plan_limit_exceeded", "Plan limit exceeded.", ErrorType.Unexpected, HttpStatusCode: HttpStatusCode.PaymentRequired));

        var result = await FunctionRunner.RunAsync(
            () => throw new InvalidOperationException("boom"),
            Logger,
            "TestFunction",
            mapper.Object);

        Assert.Equal(StatusCodes.Status402PaymentRequired, GetStatusCode(result));
    }

    [Fact]
    public async Task RunAsync_UnknownException_MapperDoesNotRecognizeIt_FallsBackToUnexpected500()
    {
        var mapper = new Mock<IExceptionToErrorMapper>();
        mapper.Setup(m => m.Map(It.IsAny<Exception>())).Returns((Error?)null);

        var result = await FunctionRunner.RunAsync(
            () => throw new InvalidOperationException("boom"),
            Logger,
            "TestFunction",
            mapper.Object);

        Assert.Equal(StatusCodes.Status500InternalServerError, GetStatusCode(result));
    }

    private static int GetStatusCode(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode
            ?? throw new InvalidOperationException("Result did not carry a status code.");
}
