using System.Net;
using System.Reflection;
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

#pragma warning disable CS0618 // These tests exercise the deprecated HttpStatusCode escape hatch (DA-010) on purpose.
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
    public void Equality_ConsidersHttpStatusCode()
    {
        var first = new Error("code", "message", HttpStatusCode: HttpStatusCode.PaymentRequired);
        var second = new Error("code", "message", HttpStatusCode: HttpStatusCode.Forbidden);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void FiveValueDeconstruct_StillReturnsHttpStatusCode()
    {
        var error = new Error("code", "message", ErrorType.NotFound, null, HttpStatusCode.Gone);

        var (code, message, type, details, httpStatusCode) = error;

        Assert.Equal(("code", "message", ErrorType.NotFound), (code, message, type));
        Assert.Null(details);
        Assert.Equal(HttpStatusCode.Gone, httpStatusCode);
    }
#pragma warning restore CS0618

    [Fact]
    public void HttpStatusCode_IsObsolete_WithoutBinaryBreak()
    {
        // DA-010: the 2.0 positional record's public members keep their exact signatures, so
        // assemblies compiled against 2.0 still bind; each one that carries HttpStatusCode warns.
        var fiveParameterConstructor = typeof(Error).GetConstructor(
            [typeof(string), typeof(string), typeof(ErrorType), typeof(IReadOnlyDictionary<string, string[]>), typeof(HttpStatusCode?)]);
        var fourParameterConstructor = typeof(Error).GetConstructor(
            [typeof(string), typeof(string), typeof(ErrorType), typeof(IReadOnlyDictionary<string, string[]>)]);
        var property = typeof(Error).GetProperty("HttpStatusCode");
        var fiveValueDeconstruct = typeof(Error).GetMethods()
            .Single(method => method.Name == nameof(Error.Deconstruct) && method.GetParameters().Length == 5);

        Assert.NotNull(fiveParameterConstructor);
        Assert.NotNull(fiveParameterConstructor.GetCustomAttribute<ObsoleteAttribute>());
        Assert.NotNull(fourParameterConstructor);
        Assert.Null(fourParameterConstructor.GetCustomAttribute<ObsoleteAttribute>());
        Assert.NotNull(property);
        Assert.NotNull(property.GetCustomAttribute<ObsoleteAttribute>());
        Assert.NotNull(property.GetMethod);
        Assert.NotNull(property.SetMethod);
        Assert.NotNull(fiveValueDeconstruct.GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public void FourValueDeconstruct_ReturnsTheNonDeprecatedValues()
    {
        var details = new Dictionary<string, string[]> { ["field"] = ["required"] };
        var error = new Error("code", "message", ErrorType.Validation, details);

        var (code, message, type, deconstructedDetails) = error;

        Assert.Equal(("code", "message", ErrorType.Validation), (code, message, type));
        Assert.Same(details, deconstructedDetails);
    }

    [Fact]
    public void RetryAfter_DefaultsToNull_AndCanBeSetWithoutChangingTheConstructor()
    {
        var withoutRetryAfter = new Error("rate_limited", "Slow down.", ErrorType.TooManyRequests);
        var withRetryAfter = withoutRetryAfter with { RetryAfter = TimeSpan.FromSeconds(30) };

        Assert.Null(withoutRetryAfter.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(30), withRetryAfter.RetryAfter);
        Assert.NotEqual(withoutRetryAfter, withRetryAfter);
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
