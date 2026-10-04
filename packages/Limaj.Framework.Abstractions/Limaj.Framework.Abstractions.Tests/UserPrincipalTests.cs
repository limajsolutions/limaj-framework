using Limaj.Framework.Abstractions.Identity;
using Xunit;

namespace Limaj.Framework.Abstractions.Tests;

/// <summary>
/// typed-user-principal DA-003/DA-005/DA-007: the base principal validates its user id without
/// echoing it, and its sealed ToString never reveals the id or any derived member value.
/// </summary>
public class UserPrincipalTests
{
    private const string SampleUserId = "8f14e45f-ceea-467f-a0e6-6c1d7b6a1f0e";
    private const string SampleEmail = "jane.doe@example.com";

    private sealed class EmailPrincipal(string? userId, string email) : UserPrincipal(userId)
    {
        public string Email { get; } = email;
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Ctor_RejectsEmptyOrWhitespaceUserId(string userId)
    {
        var exception = Assert.Throws<ArgumentException>(() => new UserPrincipal(userId));

        Assert.Equal("userId", exception.ParamName);
    }

    [Fact]
    public void Ctor_ExceptionMessage_DoesNotEchoTheRejectedValue()
    {
        var emptyException = Assert.Throws<ArgumentException>(() => new UserPrincipal(""));
        var whitespaceException = Assert.Throws<ArgumentException>(() => new UserPrincipal(" \t "));

        Assert.Equal(emptyException.Message, whitespaceException.Message);
        Assert.DoesNotContain(" \t ", whitespaceException.Message);
    }

    [Fact]
    public void Ctor_AcceptsNull_AsAnAuthenticatedNonUserCaller()
    {
        var principal = new UserPrincipal(null);

        Assert.Null(principal.UserId);
        Assert.False(principal.HasUserId);
    }

    [Fact]
    public void Ctor_AcceptsAUserId_AndRoundTripsIt()
    {
        var principal = new UserPrincipal(SampleUserId);

        Assert.Equal(SampleUserId, principal.UserId);
        Assert.True(principal.HasUserId);
    }

    [Fact]
    public void ToString_OfTheBasePrincipal_IsRedacted()
    {
        var principal = new UserPrincipal(SampleUserId);

        Assert.Equal("UserPrincipal { [redacted] }", principal.ToString());
        Assert.DoesNotContain(SampleUserId, $"{principal}");
    }

    [Fact]
    public void ToString_OfADerivedPrincipal_RevealsNeitherTheUserIdNorDerivedValues()
    {
        var principal = new EmailPrincipal(SampleUserId, SampleEmail);

        var rendered = $"{principal}";

        Assert.Equal("EmailPrincipal { [redacted] }", rendered);
        Assert.DoesNotContain(SampleUserId, rendered);
        Assert.DoesNotContain(SampleEmail, rendered);
    }
}
