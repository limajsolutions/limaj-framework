using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// DA-004: when <c>IncludeExceptionDetails</c> is explicitly <c>true</c>, logs one fixed,
/// data-free warning at startup. It never reads the environment and never stops the app.
/// </summary>
internal sealed class ExceptionDetailsStartupWarning(
    IOptions<LimajHttpErrorOptions> options,
    ILogger<LimajHttpErrorOptions> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.IncludeExceptionDetails == true)
        {
            HttpErrorLog.ExceptionDetailsExposureEnabled(logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
