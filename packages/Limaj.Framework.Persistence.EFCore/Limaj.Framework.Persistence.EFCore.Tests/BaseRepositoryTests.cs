using Limaj.Framework.Persistence.EFCore.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Limaj.Framework.Persistence.EFCore.Tests;

/// <summary>
/// Covers BaseRepository's soft-delete behavior: HasQueryFilter(e => e.IsActive) (added to
/// BaseEntityConfiguration alongside this test) makes reads exclude inactive rows by default,
/// includeInactive opts back in via IgnoreQueryFilters(), and HardDeleteByIdAsync purges
/// regardless of IsActive since it's the documented escape hatch.
/// </summary>
public class BaseRepositoryTests
{
    [Fact]
    public async Task InsertAsync_ThenSave_IsRetrievableByGetById()
    {
        await using var context = await CreateContextAsync();
        var repository = new TestEntityRepository(context);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "widget" };

        await repository.InsertAsync(entity, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);

        var loaded = await repository.GetByIdAsync(entity.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("widget", loaded!.Name);
    }

    [Fact]
    public async Task UpdateAsync_ThenSave_PersistsChanges()
    {
        await using var context = await CreateContextAsync();
        var repository = new TestEntityRepository(context);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "original" };
        await repository.InsertAsync(entity, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);

        entity.Name = "updated";
        await repository.UpdateAsync(entity, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);

        var loaded = await repository.GetByIdAsync(entity.Id, CancellationToken.None);
        Assert.Equal("updated", loaded!.Name);
    }

    [Fact]
    public async Task SoftDeleteByIdAsync_SetsIsActiveFalse_AndExcludesEntityFromDefaultReads()
    {
        await using var context = await CreateContextAsync();
        var repository = new TestEntityRepository(context);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "widget" };
        await repository.InsertAsync(entity, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);

        await repository.SoftDeleteByIdAsync(entity.Id, CancellationToken.None);

        Assert.Null(await repository.GetByIdAsync(entity.Id, CancellationToken.None));
        Assert.False(await repository.AnyAsync(e => e.Id == entity.Id, CancellationToken.None));
        Assert.Null(await repository.FirstOrDefaultAsync(e => e.Id == entity.Id, CancellationToken.None));
        Assert.Empty(await repository.FindAsync(e => e.Id == entity.Id, CancellationToken.None).ToListAsync());
        Assert.DoesNotContain(await repository.GetAllAsync().ToListAsync(), e => e.Id == entity.Id);
    }

    [Fact]
    public async Task SoftDeleteByIdAsync_WithIncludeInactiveTrue_StillFindsTheEntity()
    {
        await using var context = await CreateContextAsync();
        var repository = new TestEntityRepository(context);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "widget" };
        await repository.InsertAsync(entity, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);
        await repository.SoftDeleteByIdAsync(entity.Id, CancellationToken.None);

        Assert.True(await repository.AnyAsync(e => e.Id == entity.Id, CancellationToken.None, includeInactive: true));
        var found = await repository.FirstOrDefaultAsync(e => e.Id == entity.Id, CancellationToken.None, includeInactive: true);
        Assert.NotNull(found);
        Assert.False(found!.IsActive);
    }

    [Fact]
    public async Task RestoreByIdAsync_SetsIsActiveTrue_MakesEntityVisibleAgainByDefault()
    {
        await using var context = await CreateContextAsync();
        var repository = new TestEntityRepository(context);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "widget" };
        await repository.InsertAsync(entity, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);
        await repository.SoftDeleteByIdAsync(entity.Id, CancellationToken.None);

        await repository.RestoreByIdAsync(entity.Id, CancellationToken.None);

        var loaded = await repository.GetByIdAsync(entity.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.True(loaded!.IsActive);
    }

    [Fact]
    public async Task HardDeleteByIdAsync_OnActiveEntity_RemovesItPermanently()
    {
        await using var context = await CreateContextAsync();
        var repository = new TestEntityRepository(context);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "widget" };
        await repository.InsertAsync(entity, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);

        await repository.HardDeleteByIdAsync(entity.Id, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);

        Assert.True(await repository.FindAsync(e => e.Id == entity.Id, CancellationToken.None, includeInactive: true).ToListAsync() is []);
    }

    [Fact]
    public async Task HardDeleteByIdAsync_OnAlreadySoftDeletedEntity_StillRemovesItPermanently()
    {
        // Regression: HardDeleteByIdAsync used to look up the entity through GetByIdAsync, which
        // now applies the soft-delete filter; without IgnoreQueryFilters() here, hard-deleting an
        // already-inactive row would silently no-op.
        await using var context = await CreateContextAsync();
        var repository = new TestEntityRepository(context);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "widget" };
        await repository.InsertAsync(entity, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);
        await repository.SoftDeleteByIdAsync(entity.Id, CancellationToken.None);

        await repository.HardDeleteByIdAsync(entity.Id, CancellationToken.None);
        await repository.SaveAsync(CancellationToken.None);

        Assert.Empty(await repository.FindAsync(e => e.Id == entity.Id, CancellationToken.None, includeInactive: true).ToListAsync());
    }

    private static async Task<TestDbContext> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
        var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
