using System.Net;

namespace Limaj.Framework.Abstractions.Common;

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
/// HttpStatusCode is an additive escape hatch for hosts that map a product-specific exception
/// (via IExceptionToErrorMapper) to a status not covered by the 7 closed ErrorType values.
/// When set, it takes precedence over the ErrorType -> status mapping in ResultExtensions.
/// </summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Unexpected,
    IReadOnlyDictionary<string, string[]>? Details = null,
    HttpStatusCode? HttpStatusCode = null
);
