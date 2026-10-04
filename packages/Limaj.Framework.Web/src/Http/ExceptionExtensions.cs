using Limaj.Framework.Abstractions.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Web.Http;

public static class ExceptionExtensions
{
    /// <summary>
    /// Maps known framework exceptions to structured HTTP responses. Exceptions not covered by
    /// the standard bridge are offered to <paramref name="exceptionToErrorMapper"/> (if the host
    /// registered one) before falling back to a generic Unexpected/500 response. Written by
    /// <see cref="DefaultErrorHttpMapper"/> with default options (V2 format);
    /// <paramref name="isDevelopmentEnvironment"/> plays the role of
    /// <see cref="LimajHttpErrorOptions.IncludeExceptionDetails"/> for this call.
    /// </summary>
    public static IResult ToHttpResult(
        this Exception ex,
        ILogger logger,
        string operationName,
        IExceptionToErrorMapper? exceptionToErrorMapper = null,
        bool? isDevelopmentEnvironment = null)
    {
        var errorMapper = isDevelopmentEnvironment is null
            ? DefaultErrorHttpMapper.StaticFacadeDefault
            : new DefaultErrorHttpMapper(
                new LimajHttpErrorOptions { IncludeExceptionDetails = isDevelopmentEnvironment },
                AspNetCoreEnvironment.IsDevelopment);

        return ExceptionErrorBridge.ToHttpResult(
            ex,
            logger,
            operationName,
            ExceptionErrorBridge.DefaultBuiltInMapper,
            exceptionToErrorMapper,
            errorMapper,
            isDevelopmentEnvironment ?? AspNetCoreEnvironment.IsDevelopment());
    }
}
