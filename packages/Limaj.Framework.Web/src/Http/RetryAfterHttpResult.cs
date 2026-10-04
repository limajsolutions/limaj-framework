using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// Adds a <c>Retry-After</c> header (whole seconds) to an error response, then writes the
/// wrapped result. Returned only for an <c>Error</c> whose <c>RetryAfter</c> is set and > 0
/// (DA-007), so an error without it keeps its 2.0 concrete result type.
/// </summary>
public sealed class RetryAfterHttpResult : IResult, IStatusCodeHttpResult
{
    internal RetryAfterHttpResult(IResult innerResult, long retryAfterSeconds)
    {
        InnerResult = innerResult;
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>The error response the header is added to.</summary>
    public IResult InnerResult { get; }

    /// <summary>The header value, in whole seconds (rounded up).</summary>
    public long RetryAfterSeconds { get; }

    public int? StatusCode => (InnerResult as IStatusCodeHttpResult)?.StatusCode;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        return InnerResult.ExecuteAsync(httpContext);
    }
}
