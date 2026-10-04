using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Web.Http;
using Limaj.Framework.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// DA-004: IncludeExceptionDetails null/true/false, with the environment reader injected (no
/// process-variable mutation); only ex.Message is ever exposed; true logs a fixed startup
/// warning.
/// </summary>
public class ExceptionDetailsOptionTests
{
    public static readonly TheoryData<bool?, bool, bool> States = new()
    {
        // includeExceptionDetails, isDevelopmentEnvironment, expectMessageExposed
        { null, true, true },
        { null, false, false },
        { true, false, true },
        { false, true, false }
    };

    [Theory]
    [MemberData(nameof(States))]
    public async Task UnhandledException_ExposesTheMessageOnlyWhenTheResolvedOptionSaysSo(
        bool? includeExceptionDetails,
        bool isDevelopmentEnvironment,
        bool expectMessageExposed)
    {
        var options = new LimajHttpErrorOptions { IncludeExceptionDetails = includeExceptionDetails };
        var responder = new HttpResultResponder(
            new DefaultErrorHttpMapper(options, () => isDevelopmentEnvironment),
            new BuiltInExceptionToErrorMapper(),
            new RecordingLogger(),
            options,
            hostMapper: null,
            () => isDevelopmentEnvironment);

        var httpResult = await responder.RunAsync(() => throw new InvalidOperationException("secret detail"), "Op");

        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(expectMessageExposed ? "secret detail" : DefaultErrorHttpMapper.GenericUnexpectedMessage, response.GetString("title"));
    }

    [Theory]
    [MemberData(nameof(States))]
    public async Task UnexpectedResult_ExposesTheMessageOnlyWhenTheResolvedOptionSaysSo(
        bool? includeExceptionDetails,
        bool isDevelopmentEnvironment,
        bool expectMessageExposed)
    {
        var mapper = new DefaultErrorHttpMapper(
            new LimajHttpErrorOptions { IncludeExceptionDetails = includeExceptionDetails },
            () => isDevelopmentEnvironment);

        var response = await HttpResultExecutor.ExecuteAsync(mapper.Map(new Error("db", "secret detail", ErrorType.Unexpected)));

        Assert.Equal(expectMessageExposed ? "secret detail" : DefaultErrorHttpMapper.GenericUnexpectedMessage, response.GetString("title"));
    }

    [Theory]
    [InlineData(LimajProblemDetailsFormat.V2)]
    [InlineData(LimajProblemDetailsFormat.V3)]
    public async Task IncludeExceptionDetailsTrue_NeverExposesStackTraceInnerExceptionsOrData(LimajProblemDetailsFormat format)
    {
        var options = new LimajHttpErrorOptions { IncludeExceptionDetails = true, Format = format };
        var responder = new HttpResultResponder(
            new DefaultErrorHttpMapper(options, () => false),
            new BuiltInExceptionToErrorMapper(),
            new RecordingLogger(),
            options,
            hostMapper: null,
            () => false);
        Exception? thrown = null;

        var httpResult = await responder.RunAsync(
            () =>
            {
                try
                {
                    ThrowWithInnerAndData();
                }
                catch (Exception exception)
                {
                    thrown = exception;
                    throw;
                }

                return Task.FromResult(Microsoft.AspNetCore.Http.Results.Ok());
            },
            "Op");

        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Contains("outer message", response.RawBody);
        Assert.DoesNotContain("inner secret", response.RawBody);
        Assert.DoesNotContain("data secret", response.RawBody);
        Assert.DoesNotContain(nameof(ThrowWithInnerAndData), response.RawBody);
        Assert.NotNull(thrown!.StackTrace);
        Assert.DoesNotContain(thrown.StackTrace!.Split('\n')[0].Trim(), response.RawBody);
    }

    [Fact]
    public async Task IncludeExceptionDetailsTrue_LogsOneFixedDataFreeWarningAtStartup()
    {
        var logger = new RecordingLogger();

        await StartHostedServicesAsync(logger, includeExceptionDetails: true);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(1000, entry.EventId.Id);
        Assert.Equal(
            "LimajHttpErrorOptions.IncludeExceptionDetails is true: unhandled exception messages are returned to HTTP clients. Do not enable this in production.",
            entry.Message);
        Assert.All(entry.Properties, property => Assert.Equal("{OriginalFormat}", property.Key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task IncludeExceptionDetailsNotTrue_LogsNothingAtStartup(bool? includeExceptionDetails)
    {
        var logger = new RecordingLogger();

        await StartHostedServicesAsync(logger, includeExceptionDetails);

        Assert.Empty(logger.Entries);
    }

    private static async Task StartHostedServicesAsync(RecordingLogger logger, bool? includeExceptionDetails)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logger));
        services.AddLimajHttpErrors(options => options.IncludeExceptionDetails = includeExceptionDetails);
        await using var serviceProvider = services.BuildServiceProvider();

        foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    private static void ThrowWithInnerAndData()
    {
        var exception = new InvalidOperationException("outer message", new ArgumentException("inner secret"));
        exception.Data["key"] = "data secret";
        throw exception;
    }
}
