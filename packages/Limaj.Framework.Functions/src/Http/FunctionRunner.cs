using Limaj.Framework.Abstractions.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Functions.Http;

public static class FunctionRunner
{
    /// <summary>
    /// Executes a handler and maps unexpected exceptions to HTTP responses.
    /// Reduces repeated try/catch blocks across function endpoints. Pass the host's
    /// IExceptionToErrorMapper (resolved via DI in the function's constructor) to extend the
    /// bridge to exceptions this framework doesn't know about.
    /// </summary>
    public static async Task<IResult> RunAsync(
        Func<Task<IResult>> handler,
        ILogger logger,
        string functionName,
        IExceptionToErrorMapper? exceptionToErrorMapper = null)
    {
        try
        {
            return await handler();
        }
        catch (Exception ex)
        {
            return ex.ToHttpResult(logger, functionName, exceptionToErrorMapper);
        }
    }
}
