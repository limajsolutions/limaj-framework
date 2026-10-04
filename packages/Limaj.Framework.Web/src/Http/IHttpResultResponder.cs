using Limaj.Framework.Abstractions.Common;
using Microsoft.AspNetCore.Http;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// The injected entry point of the error pipeline (DA-002), registered by
/// <see cref="HttpErrorsServiceCollectionExtensions.AddLimajHttpErrors"/>. Same calls as the
/// static facades, but every error, from a failed result or from an exception, goes through
/// the registered <see cref="IErrorHttpMapper"/> with the configured
/// <see cref="LimajHttpErrorOptions"/>.
/// </summary>
public interface IHttpResultResponder
{
    IResult ToHttpResult<T>(Result<T> result, Func<T, IResult> onSuccess);

    IResult ToHttpResult(Result result, Func<IResult> onSuccess);

    /// <summary>
    /// Runs <paramref name="handler"/>. An exception becomes an Error (built-in exceptions,
    /// then the host's <c>IExceptionToErrorMapper</c>, then Unexpected), written by the
    /// registered <see cref="IErrorHttpMapper"/>.
    /// </summary>
    Task<IResult> RunAsync(Func<Task<IResult>> handler, string operationName);
}
