using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Functions.Http;

public static class FunctionRunner
{
    /// <summary>
    /// Executes a handler and maps unexpected exceptions to HTTP responses.
    /// Reduces repeated try/catch blocks across function endpoints.
    /// </summary>
    public static async Task<IResult> RunAsync(
        Func<Task<IResult>> handler,
        ILogger logger,
        string functionName)
    {
        try
        {
            return await handler();
        }
        catch (Exception ex)
        {
            return ex.ToHttpResult(logger, functionName);
        }
    }
}
