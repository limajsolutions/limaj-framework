using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Limaj.Framework.Persistence.EFCore.Persistence.Extensions;

/// <summary>
/// Base for IDesignTimeDbContextFactory implementations used by `dotnet ef migrations`, which
/// runs outside the host's DI container and so needs its own lightweight configuration bootstrap.
/// Reads local.settings.json (Azure Functions local dev), appsettings.json and environment
/// variables, in that precedence order.
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    public TDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        var connectionString = ResolveConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        Configure(optionsBuilder, connectionString);

        return CreateDbContext(optionsBuilder.Options);
    }

    /// <summary>Key looked up in configuration for the connection string (e.g. an Azure Functions app setting name).</summary>
    protected abstract string ConnectionStringName { get; }

    protected abstract void Configure(DbContextOptionsBuilder<TDbContext> optionsBuilder, string connectionString);

    protected abstract TDbContext CreateDbContext(DbContextOptions<TDbContext> options);

    protected virtual IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

    private string ResolveConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionStringName)
        ?? configuration[$"Values:{ConnectionStringName}"]
        ?? configuration[ConnectionStringName]
        ?? throw new InvalidOperationException(
            $"Connection string '{ConnectionStringName}' was not found in local.settings.json, appsettings.json or environment variables.");
}
