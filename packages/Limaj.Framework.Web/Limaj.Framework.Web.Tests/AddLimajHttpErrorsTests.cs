using System.Text.Json;
using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Abstractions.Errors;
using Limaj.Framework.Web.Http;
using Limaj.Framework.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// DA-002 registration and the integration case of the test strategy: AddLimajHttpErrors +
/// AddProblemDetails in a service provider, every output through IProblemDetailsService with
/// V3, and a custom IProblemDetailsWriter registered before AddProblemDetails replacing the
/// format.
/// </summary>
public class AddLimajHttpErrorsTests
{
    [Fact]
    public void AddLimajHttpErrors_RegistersNoProblemDetailsWriter()
    {
        var services = new ServiceCollection();

        services.AddLimajHttpErrors();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IProblemDetailsWriter));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IProblemDetailsService));
    }

    [Fact]
    public void AddLimajHttpErrors_ResolvesTheResponderWithTheDefaultMapper()
    {
        using var serviceProvider = BuildProvider(services => services.AddLimajHttpErrors());
        using var scope = serviceProvider.CreateScope();

        Assert.IsType<HttpResultResponder>(scope.ServiceProvider.GetRequiredService<IHttpResultResponder>());
        Assert.IsType<DefaultErrorHttpMapper>(scope.ServiceProvider.GetRequiredService<IErrorHttpMapper>());
        Assert.Equal(LimajProblemDetailsFormat.V2, scope.ServiceProvider.GetRequiredService<IOptions<LimajHttpErrorOptions>>().Value.Format);
    }

    [Fact]
    public void AddLimajHttpErrors_KeepsAHostMapperRegisteredBeforeIt()
    {
        using var serviceProvider = BuildProvider(services =>
        {
            services.AddSingleton<IErrorHttpMapper, UnprocessableByCodeMapper>();
            services.AddLimajHttpErrors();
        });

        Assert.IsType<UnprocessableByCodeMapper>(serviceProvider.GetRequiredService<IErrorHttpMapper>());
    }

    [Fact]
    public void AddLimajHttpErrors_RejectsAnUndefinedFormat()
    {
        using var serviceProvider = BuildProvider(services =>
            services.AddLimajHttpErrors(options => options.Format = (LimajProblemDetailsFormat)42));

        Assert.Throws<OptionsValidationException>(() => serviceProvider.GetRequiredService<IOptions<LimajHttpErrorOptions>>().Value);
    }

    [Fact]
    public async Task Responder_UsesTheHostsMapperForResultsAndExceptions()
    {
        await using var serviceProvider = BuildProvider(services =>
        {
            services.AddLimajHttpErrors(options => options.Format = LimajProblemDetailsFormat.V3);
            services.AddSingleton<IErrorHttpMapper, UnprocessableByCodeMapper>();
            services.AddSingleton<IExceptionToErrorMapper, OrderAlreadyShippedExceptionMapper>();
        });
        await using var scope = serviceProvider.CreateAsyncScope();
        var responder = scope.ServiceProvider.GetRequiredService<IHttpResultResponder>();

        var fromResult = responder.ToHttpResult(Result.Fail(OrderAlreadyShipped), Results.NoContent);
        var fromException = await responder.RunAsync(() => throw new OrderAlreadyShippedException(), "Op");

        var resultResponse = await HttpResultExecutor.ExecuteAsync(fromResult, scope.ServiceProvider);
        var exceptionResponse = await HttpResultExecutor.ExecuteAsync(fromException, scope.ServiceProvider);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, resultResponse.StatusCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exceptionResponse.StatusCode);
        Assert.Equal("order_already_shipped", exceptionResponse.GetString("code"));
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Unexpected)]
    [InlineData(ErrorType.TooManyRequests)]
    public async Task V3_WithAddProblemDetails_EveryErrorGoesThroughTheProblemDetailsService(ErrorType errorType)
    {
        await using var serviceProvider = BuildProvider(services =>
        {
            services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions["customized"] = true);
            services.AddLimajHttpErrors(options => options.Format = LimajProblemDetailsFormat.V3);
        });
        await using var scope = serviceProvider.CreateAsyncScope();
        var responder = scope.ServiceProvider.GetRequiredService<IHttpResultResponder>();

        var httpResult = responder.ToHttpResult(Result.Fail(new Error("code", "message", errorType)), Results.NoContent);

        var response = await HttpResultExecutor.ExecuteAsync(httpResult, scope.ServiceProvider);
        Assert.Equal("application/problem+json", response.ContentType);
        Assert.True(response.HasProperty("customized"));
        Assert.True(response.HasProperty("traceId"));
        Assert.Equal("code", response.GetString("code"));
    }

    [Fact]
    public async Task CustomProblemDetailsWriter_RegisteredBeforeAddProblemDetails_ReplacesTheFormat()
    {
        await using var serviceProvider = BuildProvider(services =>
        {
            services.AddSingleton<IProblemDetailsWriter, EnvelopeProblemDetailsWriter>();
            services.AddProblemDetails();
            services.AddLimajHttpErrors(options => options.Format = LimajProblemDetailsFormat.V3);
        });
        await using var scope = serviceProvider.CreateAsyncScope();
        var responder = scope.ServiceProvider.GetRequiredService<IHttpResultResponder>();

        var httpResult = responder.ToHttpResult(Result.Fail(new Error("user_missing", "No user.", ErrorType.NotFound)), Results.NoContent);

        var response = await HttpResultExecutor.ExecuteAsync(httpResult, scope.ServiceProvider);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.False(response.GetProperty("success").GetBoolean());
        Assert.Equal("user_missing", response.GetProperty("error").GetProperty("code").GetString());
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    // A business rule violation (valid input, a rule forbids it). Until 3.0.0 adds
    // ErrorType.BusinessRule (DA-009) it has no type of its own, so Type stays at its default
    // and the host's mapper answers 422 keyed on the code.
    private static readonly Error OrderAlreadyShipped = new("order_already_shipped", "The order has already shipped.");

    private sealed class UnprocessableByCodeMapper(IOptions<LimajHttpErrorOptions> options) : DefaultErrorHttpMapper(options)
    {
        public override IResult Map(Error error) =>
            error.Code == OrderAlreadyShipped.Code
                ? MapWithStatusCode(error, StatusCodes.Status422UnprocessableEntity)
                : base.Map(error);
    }

    private sealed class OrderAlreadyShippedException : Exception;

    private sealed class OrderAlreadyShippedExceptionMapper : IExceptionToErrorMapper
    {
        public Error? Map(Exception exception) =>
            exception is OrderAlreadyShippedException ? OrderAlreadyShipped : null;
    }

    private sealed class EnvelopeProblemDetailsWriter : IProblemDetailsWriter
    {
        public bool CanWrite(ProblemDetailsContext context) => true;

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            var code = context.ProblemDetails.Extensions.TryGetValue("code", out var value) ? value : null;
            var envelope = new { success = false, error = new { code, message = context.ProblemDetails.Detail } };
            return new ValueTask(context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(envelope)));
        }
    }
}
