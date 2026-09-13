using System.Reflection;
using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Application.Services;
using Limaj.Framework.Functions.Http;
using Limaj.Framework.Persistence.EFCore.Repositories.Base;
using NetArchTest.Rules;
using Xunit;

namespace Limaj.Framework.Architecture.Tests;

/// <summary>
/// DA-005: asserts the dependency direction table in CLAUDE.md by reflection, so a future
/// dynamic-typing/reflection-based workaround that the project-reference build check wouldn't
/// catch still fails a test. Abstractions depends on nothing; Application, Persistence.EFCore
/// and Functions may each depend only on Abstractions (plus their own declared external deps),
/// never on one another.
/// </summary>
public class PackageDependencyDirectionTests
{
    private static readonly Assembly AbstractionsAssembly = typeof(Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(BaseService<>).Assembly;
    private static readonly Assembly PersistenceEfCoreAssembly = typeof(BaseRepository<,>).Assembly;
    private static readonly Assembly FunctionsAssembly = typeof(FunctionRunner).Assembly;

    [Fact]
    public void Abstractions_DoesNotDependOnAnyOtherFrameworkPackage()
    {
        AssertNoDependencyOn(
            AbstractionsAssembly,
            ApplicationAssembly.GetName().Name!,
            PersistenceEfCoreAssembly.GetName().Name!,
            FunctionsAssembly.GetName().Name!);
    }

    [Fact]
    public void Application_OnlyDependsOnAbstractions_AmongFrameworkPackages()
    {
        AssertNoDependencyOn(
            ApplicationAssembly,
            PersistenceEfCoreAssembly.GetName().Name!,
            FunctionsAssembly.GetName().Name!);
    }

    [Fact]
    public void PersistenceEfCore_DoesNotDependOnApplicationOrFunctions()
    {
        AssertNoDependencyOn(
            PersistenceEfCoreAssembly,
            ApplicationAssembly.GetName().Name!,
            FunctionsAssembly.GetName().Name!);
    }

    [Fact]
    public void Functions_DoesNotDependOnApplicationOrPersistenceEfCore()
    {
        AssertNoDependencyOn(
            FunctionsAssembly,
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
}
