using System.Reflection;
using System.Xml.Linq;
using Limaj.Framework.Abstractions.Domain;
using Limaj.Framework.Application.Services;
using Limaj.Framework.Core;
using Limaj.Framework.Persistence.EFCore.Repositories.Base;
using Limaj.Framework.Web.Http;
using NetArchTest.Rules;
using Xunit;

namespace Limaj.Framework.Architecture.Tests;

/// <summary>
/// DA-005 (test-foundation) and DA-004 (result-core-package-split): asserts the dependency
/// direction table in CLAUDE.md by reflection, so a future dynamic-typing/reflection-based
/// workaround that the project-reference build check wouldn't catch still fails a test. Core
/// and Abstractions are sibling bottom layers that depend on nothing; Application,
/// Persistence.EFCore and Web may each depend only on Core and Abstractions (plus their own
/// declared external deps), never on one another.
/// </summary>
public class PackageDependencyDirectionTests
{
    private static readonly Assembly CoreAssembly = typeof(Result).Assembly;
    private static readonly Assembly AbstractionsAssembly = typeof(BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(BaseService<>).Assembly;
    private static readonly Assembly PersistenceEfCoreAssembly = typeof(BaseRepository<,>).Assembly;
    private static readonly Assembly WebAssembly = typeof(RequestRunner).Assembly;

    [Fact]
    public void Core_DoesNotDependOnAnyOtherFrameworkPackage()
    {
        AssertNoDependencyOn(
            CoreAssembly,
            AbstractionsAssembly.GetName().Name!,
            ApplicationAssembly.GetName().Name!,
            PersistenceEfCoreAssembly.GetName().Name!,
            WebAssembly.GetName().Name!);
    }

    [Fact]
    public void Abstractions_DoesNotDependOnAnyOtherFrameworkPackage()
    {
        AssertNoDependencyOn(
            AbstractionsAssembly,
            CoreAssembly.GetName().Name!,
            ApplicationAssembly.GetName().Name!,
            PersistenceEfCoreAssembly.GetName().Name!,
            WebAssembly.GetName().Name!);
    }

    [Fact]
    public void Application_OnlyDependsOnAbstractionsAndCore_AmongFrameworkPackages()
    {
        AssertNoDependencyOn(
            ApplicationAssembly,
            PersistenceEfCoreAssembly.GetName().Name!,
            WebAssembly.GetName().Name!);
    }

    /// <summary>
    /// Application references Core even though it uses no Core type today, so a product that
    /// references only Limaj.Framework.Application still reaches <see cref="Result"/> (DA-004).
    /// Read from the .csproj: the compiler drops a reference to an assembly whose types are
    /// unused, so Application's metadata cannot show it.
    /// </summary>
    [Fact]
    public void Application_ProjectReferencesExactlyAbstractionsAndCore()
    {
        var applicationProjectFile = Path.Combine(
            FindRepositoryRoot(),
            "packages",
            "Limaj.Framework.Application",
            "Limaj.Framework.Application.csproj");

        var referencedFrameworkProjects = XDocument.Load(applicationProjectFile)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [AbstractionsAssembly.GetName().Name!, CoreAssembly.GetName().Name!],
            referencedFrameworkProjects);
    }

    [Fact]
    public void PersistenceEfCore_DoesNotDependOnApplicationOrWeb()
    {
        AssertNoDependencyOn(
            PersistenceEfCoreAssembly,
            ApplicationAssembly.GetName().Name!,
            WebAssembly.GetName().Name!);
    }

    [Fact]
    public void Web_DoesNotDependOnApplicationOrPersistenceEfCore()
    {
        AssertNoDependencyOn(
            WebAssembly,
            ApplicationAssembly.GetName().Name!,
            PersistenceEfCoreAssembly.GetName().Name!);
    }

    private static void AssertNoDependencyOn(Assembly assembly, params string[] forbiddenDependencies)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        var offendingTypes = result.FailingTypes?.Select(type => type.FullName) ?? [];
        Assert.True(result.IsSuccessful, $"Forbidden dependency found in: {string.Join(", ", offendingTypes)}");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Limaj.Framework.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Limaj.Framework.sln not found above {AppContext.BaseDirectory}.");
    }
}
