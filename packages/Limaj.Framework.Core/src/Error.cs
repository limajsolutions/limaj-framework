using System.Net;
using System.Runtime.CompilerServices;

namespace Limaj.Framework.Core;

public enum ErrorType
{
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
    Unauthorized = 5,
    Unexpected = 6,
    TooManyRequests = 7
}

/// <summary>
/// A transport-neutral failure carried by <see cref="Result"/>/<see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// Written as an explicit record (instead of a positional one) so the deprecated
/// <see cref="HttpStatusCode"/> can warn on construction while 2.0 source keeps compiling: the
/// 5-parameter constructor and the 5-value <c>Deconstruct</c> of the 2.0 positional record are
/// kept with the same signatures, marked <see cref="ObsoleteAttribute"/>, and
/// <see cref="OverloadResolutionPriorityAttribute"/> keeps calls that don't pass
/// <c>HttpStatusCode</c> on the non-obsolete constructor. This is source compatibility only:
/// <see cref="Error"/> moved from <c>Limaj.Framework.Abstractions</c> to
/// <c>Limaj.Framework.Core</c> in 3.0.0 with no type forwarding, so assemblies compiled against
/// 2.x must be recompiled.
/// </remarks>
public sealed record Error
{
    internal const string HttpStatusCodeObsoleteMessage =
        "Error.HttpStatusCode is a transport concept and is removed in 4.0.0. Resolve statuses outside the ErrorType mapping in the host's IErrorHttpMapper (Limaj.Framework.Web), keyed on Error.Code.";

    [OverloadResolutionPriority(1)]
    public Error(
        string Code,
        string Message,
        ErrorType Type = ErrorType.Unexpected,
        IReadOnlyDictionary<string, string[]>? Details = null)
    {
        this.Code = Code;
        this.Message = Message;
        this.Type = Type;
        this.Details = Details;
    }

    [Obsolete(HttpStatusCodeObsoleteMessage)]
    public Error(
        string Code,
        string Message,
        ErrorType Type = ErrorType.Unexpected,
        IReadOnlyDictionary<string, string[]>? Details = null,
        HttpStatusCode? HttpStatusCode = null)
        : this(Code, Message, Type, Details)
    {
        this.HttpStatusCode = HttpStatusCode;
    }

    public string Code { get; init; }

    public string Message { get; init; }

    public ErrorType Type { get; init; }

    public IReadOnlyDictionary<string, string[]>? Details { get; init; }

    /// <summary>
    /// Deprecated escape hatch for a status not covered by <see cref="ErrorType"/>. When set,
    /// it still takes precedence over the ErrorType -> status mapping in 3.x.
    /// </summary>
    [Obsolete(HttpStatusCodeObsoleteMessage)]
    public HttpStatusCode? HttpStatusCode { get; init; }

    /// <summary>
    /// How long the caller should wait before retrying (typically with
    /// <see cref="ErrorType.TooManyRequests"/>). Transport-neutral: the HTTP layer turns it into
    /// a <c>Retry-After</c> header, and any other transport can read it as is.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    public void Deconstruct(
        out string Code,
        out string Message,
        out ErrorType Type,
        out IReadOnlyDictionary<string, string[]>? Details)
    {
        Code = this.Code;
        Message = this.Message;
        Type = this.Type;
        Details = this.Details;
    }

    [Obsolete(HttpStatusCodeObsoleteMessage)]
    public void Deconstruct(
        out string Code,
        out string Message,
        out ErrorType Type,
        out IReadOnlyDictionary<string, string[]>? Details,
        out HttpStatusCode? HttpStatusCode)
    {
        Deconstruct(out Code, out Message, out Type, out Details);
        HttpStatusCode = this.HttpStatusCode;
    }
}
