using Limaj.Framework.Persistence.EFCore.Persistence.Extensions;
using Limaj.Framework.Persistence.EFCore.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Limaj.Framework.Persistence.EFCore.Tests;

public class SqlServerRetryExtensionsTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=Test;Trusted_Connection=True;";

    [Fact]
    public void UseSqlServerWithRetry_Generic_ConfiguresSqlServerProviderWithRetryingExecutionStrategy()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServerWithRetry(ConnectionString)
            .Options;

        using var context = new TestDbContext(options);

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.True(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    [Fact]
    public void UseSqlServerWithRetry_NonGeneric_ConfiguresSqlServerProviderWithRetryingExecutionStrategy()
    {
        var options = new DbContextOptionsBuilder()
            .UseSqlServerWithRetry(ConnectionString)
            .Options;

        using var context = new DbContext(options);

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.True(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    [Fact]
    public void DefaultMaxRetryCount_IsFive()
    {
        Assert.Equal(5, SqlServerRetryExtensions.DefaultMaxRetryCount);
    }

    [Fact]
    public void DefaultMaxRetryDelay_IsTenSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), SqlServerRetryExtensions.DefaultMaxRetryDelay);
    }
}
