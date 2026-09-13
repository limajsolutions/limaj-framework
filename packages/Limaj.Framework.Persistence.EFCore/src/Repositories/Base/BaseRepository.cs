using Limaj.Framework.Abstractions.Contracts;
using Limaj.Framework.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Limaj.Framework.Persistence.EFCore.Repositories.Base;

public class BaseRepository<T, TDbContext> : IRepository<T>
    where T : BaseEntity
    where TDbContext : DbContext
{
    protected readonly TDbContext Context;
    protected readonly DbSet<T> Set;

    public BaseRepository(TDbContext dbContext)
    {
        Context = dbContext;
        Set = dbContext.Set<T>();
    }

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Set.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public Task InsertAsync(T entity, CancellationToken cancellationToken) =>
        Set.AddAsync(entity, cancellationToken).AsTask();

    public Task UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        Set.Update(entity);
        return Task.CompletedTask;
    }

    // Both ignore the soft-delete filter: SoftDeleteByIdAsync must still work when called twice
    // in a row, and RestoreByIdAsync's entire purpose is to target an already-inactive row that
    // the filter would otherwise make invisible to this query.
    public Task SoftDeleteByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Set.IgnoreQueryFilters().Where(entity => entity.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.IsActive, false), cancellationToken);

    public Task RestoreByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Set.IgnoreQueryFilters().Where(entity => entity.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.IsActive, true), cancellationToken);

    public async Task HardDeleteByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Ignores the soft-delete filter (the escape hatch must purge an already-inactive row
        // too) and does NOT use AsNoTracking: a tracking query lets EF Core's identity resolution
        // return an instance already tracked in this context (e.g. just inserted/updated in the
        // same unit of work) instead of conflicting with it when Remove attaches a second one.
        var entity = await Set.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is not null)
        {
            Set.Remove(entity);
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        Context.SaveChangesAsync(cancellationToken);

    public Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        var query = includeInactive ? Set.AsNoTracking().IgnoreQueryFilters() : Set.AsNoTracking();
        return query.AnyAsync(predicate, cancellationToken);
    }

    public IQueryable<T> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken,
        bool includeInactive = false) =>
        includeInactive
            ? Set.AsNoTracking().IgnoreQueryFilters().Where(predicate)
            : Set.AsNoTracking().Where(predicate);

    public Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        var query = includeInactive ? Set.AsNoTracking().IgnoreQueryFilters() : Set.AsNoTracking();
        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public IQueryable<T> GetAllAsync() => Set.AsNoTracking();
}
