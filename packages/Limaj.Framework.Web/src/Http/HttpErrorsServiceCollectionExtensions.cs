using Limaj.Framework.Core.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Limaj.Framework.Web.Http;

public static class HttpErrorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the injected error pipeline (DA-002): <see cref="IHttpResultResponder"/>, the
    /// default <see cref="IErrorHttpMapper"/>, <see cref="BuiltInExceptionToErrorMapper"/> and
    /// <see cref="LimajHttpErrorOptions"/>. Each service is added only if absent, so a host's
    /// own <see cref="IErrorHttpMapper"/> registered before this call is kept (one registered
    /// after it wins anyway). The host's <see cref="IExceptionToErrorMapper"/>, if registered,
    /// is picked up as is.
    /// </summary>
    /// <remarks>
    /// Deliberately registers no <c>IProblemDetailsWriter</c>: a custom writer only replaces the
    /// format when registered before <c>AddProblemDetails</c>, and that order is the host's.
    /// It does not call <c>AddProblemDetails</c> either.
    /// </remarks>
    public static IServiceCollection AddLimajHttpErrors(
        this IServiceCollection services,
        Action<LimajHttpErrorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services
            .AddOptions<LimajHttpErrorOptions>()
            .Validate(
                options => Enum.IsDefined(options.Format),
                $"{nameof(LimajHttpErrorOptions)}.{nameof(LimajHttpErrorOptions.Format)} must be a defined {nameof(LimajProblemDetailsFormat)} value.");

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton<BuiltInExceptionToErrorMapper>();
        services.TryAddSingleton<IErrorHttpMapper, DefaultErrorHttpMapper>();
        services.TryAddScoped<IHttpResultResponder, HttpResultResponder>();
        services.AddHostedService<ExceptionDetailsStartupWarning>();

        return services;
    }
}
