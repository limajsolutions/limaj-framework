using Limaj.Framework.Persistence.EFCore.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Limaj.Framework.Persistence.EFCore.Tests;

/// <summary>
/// Characterization tests for DA-001: when a retrying execution strategy is configured, EF Core
/// throws on SaveChanges inside a manually-opened transaction unless the whole
/// begin/act/commit sequence runs through Database.CreateExecutionStrategy().ExecuteAsync(...).
/// A custom retrying strategy reproduces the guard here without depending on a real SQL Server
/// connection (Sqlite has no EnableRetryOnFailure of its own).
/// </summary>
public class UnitOfWorkExecuteInTransactionAsyncTests
{
    [Fact]
    public async Task SaveChangesInsideManuallyOpenedTransaction_WithRetryingExecutionStrategy_ThrowsInvalidOperationException()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = CreateRetryingContext(connection);
        await context.Database.EnsureCreatedAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Widgets.Add(new Widget { Name = "widget-1" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithRetryingExecutionStrategy_CommitsWithoutThrowing()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = CreateRetryingContext(connection);
        await context.Database.EnsureCreatedAsync();
        var unitOfWork = new UnitOfWork<TestDbContext>(context);

        await unitOfWork.ExecuteInTransactionAsync(async cancellationToken =>
        {
            context.Widgets.Add(new Widget { Name = "widget-1" });
            await context.SaveChangesAsync(cancellationToken);
        });

        Assert.Equal(1, await context.Widgets.CountAsync());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenActionThrows_RollsBackAndPropagatesException()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = CreateRetryingContext(connection);
        await context.Database.EnsureCreatedAsync();
        var unitOfWork = new UnitOfWork<TestDbContext>(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(async cancellationToken =>
            {
                context.Widgets.Add(new Widget { Name = "widget-1" });
                await context.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("Simulated failure after write.");
            }));

        Assert.Equal(0, await context.Widgets.CountAsync());
    }

    private static TestDbContext CreateRetryingContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection, sqlite => sqlite.ExecutionStrategy(
                dependencies => new TestRetryingExecutionStrategy(dependencies)))
            .Options;

        return new TestDbContext(options);
    }
}
