using System.Net;
using Limaj.Framework.Abstractions.Common;
using Xunit;

namespace Limaj.Framework.Abstractions.Tests;

public class ErrorTests
{
    [Fact]
    public void Type_DefaultsToUnexpected_WhenNotSpecified()
    {
        var error = new Error("code", "message");

        Assert.Equal(ErrorType.Unexpected, error.Type);
    }

    [Fact]
    public void HttpStatusCode_DefaultsToNull_WhenNotSpecified()
    {
        var error = new Error("code", "message");

        Assert.Null(error.HttpStatusCode);
    }

    [Fact]
    public void HttpStatusCode_CanBeSetExplicitly()
    {
        var error = new Error("plan_limit_exceeded", "message", HttpStatusCode: HttpStatusCode.PaymentRequired);

        Assert.Equal(HttpStatusCode.PaymentRequired, error.HttpStatusCode);
    }

    [Fact]
    public void Details_DefaultsToNull_WhenNotSpecified()
    {
        var error = new Error("code", "message");

        Assert.Null(error.Details);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var first = new Error("code", "message", ErrorType.Validation);
        var second = new Error("code", "message", ErrorType.Validation);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Equality_ConsidersHttpStatusCode()
    {
        var first = new Error("code", "message", HttpStatusCode: HttpStatusCode.PaymentRequired);
        var second = new Error("code", "message", HttpStatusCode: HttpStatusCode.Forbidden);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ErrorType_RemainsClosedToTheSevenDocumentedValues()
    {
        // CLAUDE.md and DA-003 both make this an explicit, permanent constraint: extensibility
        // for new statuses comes from Error.HttpStatusCode + IExceptionToErrorMapper, never from
        // growing this enum. If this test fails, a member was added and the docs/mapping need a
        // matching, deliberate decision — not an incidental enum change.
        var values = Enum.GetValues<ErrorType>();

        Assert.Equal(7, values.Length);
        Assert.Equal(
            [
                ErrorType.Validation,
                ErrorType.NotFound,
                ErrorType.Conflict,
                ErrorType.Forbidden,
                ErrorType.Unauthorized,
                ErrorType.Unexpected,
                ErrorType.TooManyRequests
            ],
            values);
    }
}
