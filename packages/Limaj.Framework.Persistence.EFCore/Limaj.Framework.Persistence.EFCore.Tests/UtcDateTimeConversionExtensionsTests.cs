using Limaj.Framework.Persistence.EFCore.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Limaj.Framework.Persistence.EFCore.Tests;

public class UtcDateTimeConversionExtensionsTests
{
    [Fact]
    public async Task UseUtcDateTimeConversion_RoundTripsDateTimeAsUtc_RegardlessOfStoredKind()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;

        await using (var writeContext = new TestDbContext(options))
        {
            await writeContext.Database.EnsureCreatedAsync();
            writeContext.TestEntities.Add(new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "entity",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2026, 1, 1, 12, 0, 0), DateTimeKind.Local),
                UpdatedAt = DateTime.SpecifyKind(new DateTime(2026, 1, 1, 12, 0, 0), DateTimeKind.Unspecified)
            });
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new TestDbContext(options);
        var reloaded = await readContext.TestEntities.AsNoTracking().FirstAsync();

        Assert.Equal(DateTimeKind.Utc, reloaded.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, reloaded.UpdatedAt.Kind);
    }
}
