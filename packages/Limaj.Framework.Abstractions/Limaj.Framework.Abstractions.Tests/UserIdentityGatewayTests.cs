using Limaj.Framework.Abstractions.Identity;
using Xunit;

namespace Limaj.Framework.Abstractions.Tests;

/// <summary>
/// typed-user-principal DA-002/DA-003/DA-006/DA-007: the [Obsolete] default members derive from
/// the single principal snapshot, and the generic gateway serves the non-generic contract with
/// the very same instance.
/// </summary>
public class UserIdentityGatewayTests
{
    private const string SampleUserId = "8f14e45f-ceea-467f-a0e6-6c1d7b6a1f0e";

    private sealed class TenantPrincipal(string? userId, string tenantId) : UserPrincipal(userId)
    {
        public string TenantId { get; } = tenantId;
    }

    /// <summary>Implements only the new member, as a 3.0.0 implementer does.</summary>
    private sealed class FakeUserIdentityGateway(UserPrincipal? principal) : IUserIdentityGateway
    {
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public Task<UserPrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default)
        {
            ReceivedTokens.Add(cancellationToken);
            return Task.FromResult(principal);
        }
    }

    private sealed class FakeTenantIdentityGateway(TenantPrincipal? principal) : IUserIdentityGateway<TenantPrincipal>
    {
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public Task<TenantPrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default)
        {
            ReceivedTokens.Add(cancellationToken);
            return Task.FromResult(principal);
        }
    }

#pragma warning disable CS0618 // DA-006: the obsolete members are the subject under test.
    [Theory]
    [InlineData(false, null, null, false)]                // anonymous: null principal
    [InlineData(true, null, null, true)]                  // authenticated non-user caller
    [InlineData(true, SampleUserId, SampleUserId, true)]  // authenticated user
    public async Task ObsoleteMembers_DeriveUserIdAndAuthenticationFromThePrincipal(
        bool hasPrincipal, string? principalUserId, string? expectedUserId, bool expectedIsAuthenticated)
    {
        var principal = hasPrincipal ? new UserPrincipal(principalUserId) : null;
        IUserIdentityGateway gateway = new FakeUserIdentityGateway(principal);

        Assert.Equal(expectedUserId, await gateway.GetCurrentUserIdAsync());
        Assert.Equal(expectedIsAuthenticated, await gateway.IsAuthenticatedAsync());
    }

    [Fact]
    public async Task ObsoleteMembers_PassTheCancellationTokenThrough()
    {
        var fakeGateway = new FakeUserIdentityGateway(new UserPrincipal(SampleUserId));
        IUserIdentityGateway gateway = fakeGateway;
        using var userIdTokenSource = new CancellationTokenSource();
        using var isAuthenticatedTokenSource = new CancellationTokenSource();

        await gateway.GetCurrentUserIdAsync(userIdTokenSource.Token);
        await gateway.IsAuthenticatedAsync(isAuthenticatedTokenSource.Token);

        Assert.Equal([userIdTokenSource.Token, isAuthenticatedTokenSource.Token], fakeGateway.ReceivedTokens);
    }
#pragma warning restore CS0618

    [Fact]
    public async Task GenericGateway_ServesTheNonGenericContractWithTheSameInstance()
    {
        var tenantPrincipal = new TenantPrincipal(SampleUserId, "tenant-42");
        var fakeGateway = new FakeTenantIdentityGateway(tenantPrincipal);
        IUserIdentityGateway gateway = fakeGateway;
        using var tokenSource = new CancellationTokenSource();

        var principal = await gateway.GetCurrentPrincipalAsync(tokenSource.Token);

        Assert.Same(tenantPrincipal, principal);
        Assert.Equal([tokenSource.Token], fakeGateway.ReceivedTokens);
    }

    [Fact]
    public async Task GenericGateway_ServesTheNonGenericContractWithNull_ForAnonymous()
    {
        IUserIdentityGateway gateway = new FakeTenantIdentityGateway(null);

        Assert.Null(await gateway.GetCurrentPrincipalAsync());
    }
}
