using Limaj.Framework.Core;
using Limaj.Framework.Core.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// The one exception path shared by the static facade and the injected responder (DA-002):
/// exception -> Error (built-ins, then the host's mapper, then Unexpected) -> IErrorHttpMapper.
/// </summary>
internal static class ExceptionErrorBridge
{
    /// <summary>DA-005: the code of an unhandled exception's 500 (never <c>ex.Source</c>).</summary>
    internal const string UnhandledExceptionCode = "unexpected_error";

    internal static readonly BuiltInExceptionToErrorMapper DefaultBuiltInMapper = new();

    internal static IResult ToHttpResult(
        Exception exception,
        ILogger logger,
        string operationName,
        BuiltInExceptionToErrorMapper builtInMapper,
        IExceptionToErrorMapper? hostMapper,
        IErrorHttpMapper errorMapper,
        bool includeExceptionDetails)
    {
        if (builtInMapper.Map(exception) is { } builtInError)
        {
            LogBuiltInError(logger, operationName, exception, builtInError);
            return errorMapper.Map(builtInError);
        }

        if (hostMapper?.Map(exception) is { } hostError)
        {
            LogMappedError(logger, operationName, exception, hostError);
            return errorMapper.Map(hostError);
        }

        // ex.Source and ex.Message stay in the log. The client gets a fixed code and, unless
        // exception details are enabled, a generic message (DA-004/DA-005). Only ex.Message is
        // ever exposed: never the stack trace, inner exceptions or Exception.Data.
        HttpErrorLog.UnhandledException(logger, exception, operationName, exception.Source);
        var clientMessage = includeExceptionDetails ? exception.Message : DefaultErrorHttpMapper.GenericUnexpectedMessage;
        return errorMapper.Map(new Error(UnhandledExceptionCode, clientMessage, ErrorType.Unexpected));
    }

    private static void LogBuiltInError(ILogger logger, string operationName, Exception exception, Error error)
    {
        switch (exception)
        {
            case DomainValidationException:
                HttpErrorLog.ValidationFailed(logger, operationName, exception.Message);
                break;
            case NotFoundException notFoundException:
                HttpErrorLog.ResourceNotFound(logger, operationName, notFoundException.Resource, exception.Message);
                break;
            case ConflictException:
                HttpErrorLog.Conflict(logger, operationName, exception.Message);
                break;
            default:
                // A host subclass of BuiltInExceptionToErrorMapper mapped something else.
                LogMappedError(logger, operationName, exception, error);
                break;
        }
    }

    /// <summary>
    /// DA-006: logged by the resulting status, classified from the Error (not the IResult) so
    /// it needs no HttpContext: 5xx at Error, 4xx at Warning, both with the exception.
    /// </summary>
    private static void LogMappedError(ILogger logger, string operationName, Exception exception, Error error)
    {
        var statusCode = DefaultErrorHttpMapper.ResolveStatusCode(error);
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            HttpErrorLog.MappedServerError(logger, exception, operationName, error.Code, statusCode);
        }
        else
        {
            HttpErrorLog.MappedClientError(logger, exception, operationName, error.Code, statusCode);
        }
    }
}
