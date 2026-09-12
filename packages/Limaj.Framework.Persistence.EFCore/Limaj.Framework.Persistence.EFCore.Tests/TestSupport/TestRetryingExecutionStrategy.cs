using Microsoft.EntityFrameworkCore.Storage;

namespace Limaj.Framework.Persistence.EFCore.Tests.TestSupport;

/// <summary>
/// Stands in for a provider-specific retrying strategy (e.g. SqlServerRetryingExecutionStrategy)
/// so the retry-transaction guard behavior can be reproduced against Sqlite in tests.
/// </summary>
internal sealed class TestRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 1, maxRetryDelay: TimeSpan.Zero)
{
    protected override bool ShouldRetryOn(Exception exception) => false;
}
