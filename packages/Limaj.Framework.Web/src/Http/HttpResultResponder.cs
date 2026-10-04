using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Abstractions.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Limaj.Framework.Web.Http;

internal sealed class HttpResultResponder : IHttpResultResponder
{
    private readonly IErrorHttpMapper _errorMapper;
    private readonly BuiltInExceptionToErrorMapper _builtInMapper;
    private readonly IExceptionToErrorMapper? _hostMapper;
    private readonly ILogger _logger;
    private readonly LimajHttpErrorOptions _options;
    private readonly Func<bool> _isDevelopmentEnvironment;

    public HttpResultResponder(
        IErrorHttpMapper errorMapper,
        BuiltInExceptionToErrorMapper builtInMapper,
        ILogger<HttpResultResponder> logger,
        IOptions<LimajHttpErrorOptions> options,
        IExceptionToErrorMapper? hostMapper = null)
        : this(errorMapper, builtInMapper, logger, options.Value, hostMapper, AspNetCoreEnvironment.IsDevelopment)
    {
    }

    internal HttpResultResponder(
        IErrorHttpMapper errorMapper,
        BuiltInExceptionToErrorMapper builtInMapper,
        ILogger logger,
        LimajHttpErrorOptions options,
        IExceptionToErrorMapper? hostMapper,
        Func<bool> isDevelopmentEnvironment)
    {
        _errorMapper = errorMapper;
        _builtInMapper = builtInMapper;
        _hostMapper = hostMapper;
        _logger = logger;
        _options = options;
        _isDevelopmentEnvironment = isDevelopmentEnvironment;
    }

    public IResult ToHttpResult<T>(Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? onSuccess(result.Value!) : _errorMapper.Map(result.Error!);
    }

    public IResult ToHttpResult(Result result, Func<IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? onSuccess() : _errorMapper.Map(result.Error!);
    }

    public async Task<IResult> RunAsync(Func<Task<IResult>> handler, string operationName)
    {
        try
        {
            return await handler();
        }
        catch (Exception exception)
        {
            return ExceptionErrorBridge.ToHttpResult(
                exception,
                _logger,
                operationName,
                _builtInMapper,
                _hostMapper,
                _errorMapper,
                _options.ResolveIncludeExceptionDetails(_isDevelopmentEnvironment));
        }
    }
}
