using Limaj.Framework.Persistence.EFCore.Persistence.Extensions;
using Limaj.Framework.Persistence.EFCore.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Limaj.Framework.Persistence.EFCore.Tests;

public class DesignTimeDbContextFactoryBaseTests
{
    private class TestFactory(IConfiguration configuration) : DesignTimeDbContextFactoryBase<TestDbContext>
    {
        protected override string ConnectionStringName => "TestDb";

        protected override IConfiguration BuildConfiguration() => configuration;

        protected override void Configure(DbContextOptionsBuilder<TestDbContext> optionsBuilder, string connectionString) =>
            optionsBuilder.UseSqlite(connectionString);

        protected override TestDbContext CreateDbContext(DbContextOptions<TestDbContext> options) => new(options);
    }

    [Fact]
    public void CreateDbContext_WithConfiguredConnectionString_ReturnsContextConfiguredWithIt()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TestDb"] = "DataSource=:memory:"
            })
            .Build();
        var factory = new TestFactory(configuration);

        using var context = factory.CreateDbContext([]);

        Assert.IsType<TestDbContext>(context);
        Assert.Contains("DataSource=:memory:", context.Database.GetConnectionString());
    }

    [Fact]
    public void CreateDbContext_WithoutConfiguredConnectionString_ThrowsInvalidOperationException()
    {
        var configuration = new ConfigurationBuilder().Build();
        var factory = new TestFactory(configuration);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
        Assert.Contains("TestDb", exception.Message);
    }
}
