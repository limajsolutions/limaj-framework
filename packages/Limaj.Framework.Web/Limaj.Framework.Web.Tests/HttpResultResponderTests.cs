using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Abstractions.Errors;
using Limaj.Framework.Web.Http;
using Limaj.Framework.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// DA-002/DA-006: the injected entry point routes failed results and exceptions through the
/// same IErrorHttpMapper, keeps the built-in exceptions ahead of a host catch-all, and logs a
/// mapped error by its resulting status with stable EventIds.
/// </summary>
public class HttpResultResponderTests
{
    private readonly RecordingErrorHttpMapper _errorMapper = new();
    private readonly RecordingLogger _logger = new();

    [Fact]
    public void ToHttpResult_Success_InvokesCallbackWithoutTheMapper()
    {
        var responder = CreateResponder();

        var httpResult = responder.ToHttpResult(Result<int>.Ok(42), value => Results.Ok(value));

        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)httpResult).StatusCode);
        Assert.Empty(_errorMapper.MappedErrors);
    }

    [Fact]
    public void ToHttpResult_Failure_GoesThroughTheRegisteredMapper()
    {
        var responder = CreateResponder();
        var error = new Error("order_already_exists", "An order with this number already exists.", ErrorType.Conflict);

        responder.ToHttpResult(Result.Fail(error), Results.NoContent);
        responder.ToHttpResult(Result<int>.Fail(error), value => Results.Ok(value));

        Assert.Equal([error, error], _errorMapper.MappedErrors);
    }

    [Fact]
    public async Task RunAsync_Success_ReturnsTheHandlerResult()
    {
        var responder = CreateResponder();

        var httpResult = await responder.RunAsync(() => Task.FromResult(Results.Ok()), "Op");

        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)httpResult).StatusCode);
    }

    [Fact]
    public async Task RunAsync_HostMappedException_GoesThroughTheSameMapper()
    {
        var hostError = new Error("tenant_access_denied", "You do not have access to this tenant.", ErrorType.Forbidden);
        var responder = CreateResponder(new CatchAllExceptionMapper(hostError));

        await responder.RunAsync(() => throw new InvalidOperationException("boom"), "Op");

        Assert.Equal([hostError], _errorMapper.MappedErrors);
    }

    [Fact]
    public async Task RunAsync_UnhandledException_GoesThroughTheSameMapperAsUnexpectedError()
    {
        var responder = CreateResponder();

        await responder.RunAsync(() => throw new InvalidOperationException("boom"), "Op");

        var mappedError = Assert.Single(_errorMapper.MappedErrors);
        Assert.Equal(ErrorType.Unexpected, mappedError.Type);
        Assert.Equal("unexpected_error", mappedError.Code);
    }

    public static readonly TheoryData<Exception, ErrorType> BuiltInExceptions = new()
    {
        { new DomainValidationException(new Dictionary<string, string[]> { ["f"] = ["x"] }), ErrorType.Validation },
        { new NotFoundException("User"), ErrorType.NotFound },
        { new ConflictException("Exists."), ErrorType.Conflict }
    };

    [Theory]
    [MemberData(nameof(BuiltInExceptions))]
    public async Task RunAsync_BuiltInException_IsNotShadowedByAHostCatchAllMapper(Exception exception, ErrorType expectedType)
    {
        var responder = CreateResponder(new CatchAllExceptionMapper(new Error("catch_all", "Catch-all.", ErrorType.Unexpected)));

        await responder.RunAsync(() => throw exception, "Op");

        Assert.Equal(expectedType, Assert.Single(_errorMapper.MappedErrors).Type);
    }

    [Fact]
    public async Task RunAsync_HostMappedServerError_IsLoggedAtErrorWithTheException()
    {
        var responder = CreateResponder(new CatchAllExceptionMapper(new Error("db_down", "Database unavailable.", ErrorType.Unexpected)));
        var exception = new InvalidOperationException("boom");

        await responder.RunAsync(() => throw exception, "Op");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(5000, entry.EventId.Id);
        Assert.Equal("MappedServerError", entry.EventId.Name);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public async Task RunAsync_HostMappedClientError_IsLoggedAtWarningWithTheException()
    {
        var responder = CreateResponder(new CatchAllExceptionMapper(new Error("tenant_access_denied", "You do not have access to this tenant.", ErrorType.Forbidden)));
        var exception = new InvalidOperationException("boom");

        await responder.RunAsync(() => throw exception, "Op");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(4003, entry.EventId.Id);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public async Task RunAsync_HostMappedError_ClassifiesByTheDeprecatedExplicitStatusWhenSet()
    {
        // TooManyRequests alone resolves to 429 (Warning); only the explicit 503 makes it a 5xx.
#pragma warning disable CS0618 // The deprecated escape hatch still drives the resulting status in 2.x.
        var serviceUnavailable = new Error("server_at_capacity", "The server is at capacity. Try again later.", ErrorType.TooManyRequests, null, System.Net.HttpStatusCode.ServiceUnavailable);
#pragma warning restore CS0618
        var responder = CreateResponder(new CatchAllExceptionMapper(serviceUnavailable));

        await responder.RunAsync(() => throw new InvalidOperationException("boom"), "Op");

        Assert.Equal(LogLevel.Error, Assert.Single(_logger.Entries).Level);
    }

    [Fact]
    public async Task RunAsync_UnhandledException_IsLoggedAtErrorWithItsSource()
    {
        var responder = CreateResponder();

        await responder.RunAsync(() => throw new InvalidOperationException("boom"), "Op");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(5001, entry.EventId.Id);
        Assert.Contains(entry.Properties, property => property.Key == "Source" && Equals(property.Value, typeof(HttpResultResponderTests).Assembly.GetName().Name));
    }

    [Fact]
    public async Task RunAsync_BuiltInValidation_IsLoggedAtWarningBelowTheServerErrorIds()
    {
        var responder = CreateResponder();

        await responder.RunAsync(() => throw new DomainValidationException("Invalid."), "Op");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(4000, entry.EventId.Id);
    }

    [Fact]
    public void StaticFacade_HostMappedServerError_IsAlsoLoggedAtError()
    {
        var exception = new InvalidOperationException("boom");

        exception.ToHttpResult(_logger, "Op", new CatchAllExceptionMapper(new Error("db_down", "Down.", ErrorType.Unexpected)));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(5000, entry.EventId.Id);
    }

    [Fact]
    public void BuiltInExceptionToErrorMapper_MapsTheThreeFrameworkExceptionsAndNothingElse()
    {
        var builtInMapper = new BuiltInExceptionToErrorMapper();

        var validation = builtInMapper.Map(new DomainValidationException(new Dictionary<string, string[]> { ["f"] = ["x"] }, "Invalid."));
        var notFound = builtInMapper.Map(new NotFoundException("User"));
        var conflict = builtInMapper.Map(new ConflictException("Exists."));

        Assert.Equal(("validation_failed", "Invalid.", ErrorType.Validation), (validation!.Code, validation.Message, validation.Type));
        Assert.Equal(["x"], validation.Details!["f"]);
        Assert.Equal(("User", "User was not found.", ErrorType.NotFound), (notFound!.Code, notFound.Message, notFound.Type));
        Assert.Equal(("conflict", "Exists.", ErrorType.Conflict), (conflict!.Code, conflict.Message, conflict.Type));
        Assert.Null(builtInMapper.Map(new InvalidOperationException("boom")));
    }

    private HttpResultResponder CreateResponder(IExceptionToErrorMapper? hostMapper = null) =>
        new(_errorMapper, new BuiltInExceptionToErrorMapper(), _logger, new LimajHttpErrorOptions(), hostMapper, () => false);

    private sealed class RecordingErrorHttpMapper : IErrorHttpMapper
    {
        public List<Error> MappedErrors { get; } = [];

        public IResult Map(Error error)
        {
            MappedErrors.Add(error);
            return Results.StatusCode(StatusCodes.Status418ImATeapot);
        }
    }

    private sealed class CatchAllExceptionMapper(Error error) : IExceptionToErrorMapper
    {
        public Error? Map(Exception exception) => error;
    }
}
