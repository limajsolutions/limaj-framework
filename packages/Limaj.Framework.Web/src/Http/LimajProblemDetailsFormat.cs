namespace Limaj.Framework.Web.Http;

/// <summary>
/// The Problem Details contract every error response is written in (DA-003). A single
/// selector, not independent flags: the items below all change the same response body.
/// </summary>
public enum LimajProblemDetailsFormat
{
    /// <summary>
    /// The 2.0 output, unchanged: <c>title = Error.Message</c>, <c>detail = Error.Code</c>
    /// (no code at all on validation responses), and 404/409 written as plain JSON outside
    /// <c>IProblemDetailsService</c>. Default in 2.x; kept as the migration value in 3.0.0.
    /// </summary>
    V2 = 0,

    /// <summary>
    /// The named 3.0 contract: <c>code = Error.Code</c> on every error response (validation
    /// included), <c>detail = Error.Message</c>, <c>title</c> by status, <c>retryAfter</c> in
    /// seconds when set, and every error (404/409 included) written through
    /// <c>Results.Problem</c>/<c>IProblemDetailsService</c>.
    /// </summary>
    V3 = 1
}
