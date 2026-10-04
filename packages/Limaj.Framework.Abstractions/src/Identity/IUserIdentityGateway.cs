namespace Limaj.Framework.Abstractions.Identity;

/// <summary>
/// Resolves the authenticated caller of the current operation. Application code depends on this
/// contract (or on <see cref="IUserIdentityGateway{TPrincipal}"/>), never on <c>HttpContext</c>,
/// <c>ClaimsPrincipal</c> or other host identity types: the product's host implements it and
/// decides which claim is the user id.
/// </summary>
/// <remarks>
/// Register the implementation with a scoped lifetime (one per request or operation) and never
/// cache a principal in a singleton.
/// </remarks>
public interface IUserIdentityGateway
{
    /// <summary>
    /// Returns a snapshot of the current caller: <see langword="null"/> exactly when there is no
    /// authenticated caller, otherwise the authenticated principal (whose
    /// <see cref="UserPrincipal.UserId"/> is <see langword="null"/> for a non-user caller).
    /// </summary>
    Task<UserPrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the current user's id, derived from <see cref="GetCurrentPrincipalAsync"/>.</summary>
    /// <remarks>
    /// Do not implement this member: a class's own implementation silently replaces this default
    /// and can disagree with <see cref="GetCurrentPrincipalAsync"/>.
    /// </remarks>
    [Obsolete("Use GetCurrentPrincipalAsync(). Removed in 4.0.0.")]
    async Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
        => (await GetCurrentPrincipalAsync(cancellationToken).ConfigureAwait(false))?.UserId;

    /// <summary>Returns whether there is an authenticated caller, derived from <see cref="GetCurrentPrincipalAsync"/>.</summary>
    /// <remarks>
    /// Do not implement this member: a class's own implementation silently replaces this default
    /// and can disagree with <see cref="GetCurrentPrincipalAsync"/>.
    /// </remarks>
    [Obsolete("Use GetCurrentPrincipalAsync(). Removed in 4.0.0.")]
    async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
        => await GetCurrentPrincipalAsync(cancellationToken).ConfigureAwait(false) is not null;
}

/// <summary>
/// Resolves the authenticated caller as the product's own principal type. Implementing it also
/// implements <see cref="IUserIdentityGateway"/>, so framework or generic Application code can
/// read the user id without knowing <typeparamref name="TPrincipal"/>.
/// </summary>
/// <typeparam name="TPrincipal">The product's principal, derived from <see cref="UserPrincipal"/>.</typeparam>
public interface IUserIdentityGateway<TPrincipal> : IUserIdentityGateway
    where TPrincipal : UserPrincipal
{
    /// <inheritdoc cref="IUserIdentityGateway.GetCurrentPrincipalAsync"/>
    new Task<TPrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default);

    /// <summary>Serves the non-generic contract with the same instance (<see cref="Task{TResult}"/> is not covariant).</summary>
    async Task<UserPrincipal?> IUserIdentityGateway.GetCurrentPrincipalAsync(CancellationToken cancellationToken)
        => await GetCurrentPrincipalAsync(cancellationToken).ConfigureAwait(false);
}
