namespace Limaj.Framework.Abstractions.Identity;

public interface IUserIdentityGateway
{
    Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
}
