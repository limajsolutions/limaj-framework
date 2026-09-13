using Microsoft.EntityFrameworkCore;

namespace Limaj.Framework.Persistence.EFCore.Persistence.Extensions;

/// <summary>
/// Wraps UseSqlServer with EnableRetryOnFailure already configured, so consumers get a
/// resilient connection without having to remember DA-001's rule: transactional code on a
/// retry-enabled context must run through Database.CreateExecutionStrategy().ExecuteAsync(...)
/// (see UnitOfWork&lt;TDbContext&gt;).
/// </summary>
public static class SqlServerRetryExtensions
{
    public const int DefaultMaxRetryCount = 5;
    public static readonly TimeSpan DefaultMaxRetryDelay = TimeSpan.FromSeconds(10);

    public static DbContextOptionsBuilder<TContext> UseSqlServerWithRetry<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        int maxRetryCount = DefaultMaxRetryCount,
        TimeSpan? maxRetryDelay = null)
        where TContext : DbContext =>
        optionsBuilder.UseSqlServer(connectionString, sqlServerOptions =>
            sqlServerOptions.EnableRetryOnFailure(maxRetryCount, maxRetryDelay ?? DefaultMaxRetryDelay, errorNumbersToAdd: null));

    public static DbContextOptionsBuilder UseSqlServerWithRetry(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        int maxRetryCount = DefaultMaxRetryCount,
        TimeSpan? maxRetryDelay = null) =>
        optionsBuilder.UseSqlServer(connectionString, sqlServerOptions =>
            sqlServerOptions.EnableRetryOnFailure(maxRetryCount, maxRetryDelay ?? DefaultMaxRetryDelay, errorNumbersToAdd: null));
}
