using System.Diagnostics.CodeAnalysis;

namespace Limaj.Framework.Abstractions.Identity;

/// <summary>
/// The authenticated caller of the current operation, as resolved by an
/// <see cref="IUserIdentityGateway"/>. A gateway returns <see langword="null"/> when there is no
/// authenticated caller, so an instance of this type always means "authenticated".
/// </summary>
/// <remarks>
/// <para>
/// Products extend it with what they need (for example a <c>TenantId</c>) and expose it through
/// <see cref="IUserIdentityGateway{TPrincipal}"/>. Carry the minimum: prefer identifiers to
/// personal data, and keep e-mail, name or other directly identifying data out of the principal
/// unless the product truly needs it (data minimization — LGPD art. 6 III; GDPR art. 5(1)(c)).
/// </para>
/// <para>
/// <see cref="UserId"/> is pseudonymous data, which is still personal data (LGPD art. 5 I and
/// art. 13 §4; GDPR Recital 26). <see cref="ToString"/> is sealed and redacting, so interpolating
/// a principal into a message or a log placeholder never reveals its values; but structured-log
/// destructuring (e.g. <c>{@Principal}</c>) and JSON serialization bypass <see cref="ToString"/>,
/// so never destructure or serialize a principal into logs, and never return it raw from an
/// endpoint. Logging a user id is always an explicit choice of the caller.
/// </para>
/// </remarks>
public class UserPrincipal
{
    /// <summary>Creates a principal for an authenticated caller.</summary>
    /// <param name="userId">
    /// The opaque, stable identifier the identity provider assigns to the user — never an e-mail
    /// or a name — or <see langword="null"/> for an authenticated caller that is not a user
    /// (service-to-service, client credentials, a background job the product's gateway admits).
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="userId"/> is not <see langword="null"/> but is empty or whitespace. The
    /// message does not echo the rejected value.
    /// </exception>
    public UserPrincipal(string? userId)
    {
        if (userId is not null && string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "A user id must be null (non-user caller) or a non-empty, non-whitespace identifier.",
                nameof(userId));
        }

        UserId = userId;
    }

    /// <summary>
    /// The opaque, stable identifier the identity provider assigns to the user, or
    /// <see langword="null"/> for an authenticated non-user caller. Ownership checks against it
    /// therefore fail closed for non-user callers. Personal data: do not log it, put it in an
    /// <c>Error</c> or an exception message, or return it in a response without a reason.
    /// </summary>
    public string? UserId { get; }

    /// <summary>
    /// <see langword="true"/> when the caller is a user, i.e. <see cref="UserId"/> is not
    /// <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(UserId))]
    public bool HasUserId => UserId is not null;

    /// <summary>
    /// Returns <c>{TypeName} { [redacted] }</c>: neither <see cref="UserId"/> nor any member of a
    /// derived principal appears, so a principal interpolated into a string or a log placeholder
    /// leaks nothing. Sealed so derived principals cannot reintroduce their values.
    /// </summary>
    public sealed override string ToString() => $"{GetType().Name} {{ [redacted] }}";
}
