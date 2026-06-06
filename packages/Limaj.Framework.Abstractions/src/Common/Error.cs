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

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Unexpected,
    IReadOnlyDictionary<string, string[]>? Details = null
);
