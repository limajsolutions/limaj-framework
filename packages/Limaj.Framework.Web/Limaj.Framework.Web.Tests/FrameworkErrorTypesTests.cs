using Limaj.Framework.Abstractions.Errors;
using Limaj.Framework.Web.Http;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// DA-003 negative test: the framework must only ever expose the extension mechanism
/// (IExceptionToErrorMapper) for product-specific errors, never a concrete domain exception
/// type of its own beyond the 3 already documented in CLAUDE.md. A product type such as
/// "PlanLimitExceededException" must live in the host, not in Limaj.Framework.*.
/// </summary>
public class FrameworkErrorTypesTests
{
    private static readonly string[] KnownFrameworkExceptionTypeNames =
    [
        typeof(ConflictException).FullName!,
        typeof(DomainValidationException).FullName!,
        typeof(NotFoundException).FullName!
    ];

    [Fact]
    public void AbstractionsAssembly_DefinesOnlyTheDocumentedClosedSetOfExceptionTypes()
    {
        AssertOnlyKnownExceptionTypes(typeof(IExceptionToErrorMapper).Assembly);
    }

    [Fact]
    public void WebAssembly_DefinesNoExceptionTypesOfItsOwn()
    {
        AssertOnlyKnownExceptionTypes(typeof(RequestRunner).Assembly);
    }

    private static void AssertOnlyKnownExceptionTypes(System.Reflection.Assembly assembly)
    {
        var exceptionTypeNames = assembly.GetTypes()
            .Where(type => typeof(Exception).IsAssignableFrom(type) && type != typeof(Exception))
            .Select(type => type.FullName!)
            .ToArray();

        Assert.All(exceptionTypeNames, name => Assert.Contains(name, KnownFrameworkExceptionTypeNames));
    }
}
