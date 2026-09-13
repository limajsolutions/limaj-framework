using Limaj.Framework.Abstractions.Common;

namespace Limaj.Framework.Abstractions.Errors;

/// <summary>
/// Extension point a host resolves via DI to map exceptions the standard bridge
/// (DomainValidationException/NotFoundException/ConflictException) doesn't cover, without
/// growing the closed ErrorType enum or leaking product-specific exception types into the
/// framework. Implementations live in the host/product, never inside Limaj.Framework.*.
/// </summary>
public interface IExceptionToErrorMapper
{
    /// <summary>
    /// Returns the mapped <see cref="Error"/> for the given exception, or null if this mapper
    /// doesn't recognize it — callers should fall back to the default Unexpected mapping.
    /// </summary>
    Error? Map(Exception exception);
}
