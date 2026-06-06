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

    public Task SoftDeleteByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Set.Where(entity => entity.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.IsActive, false), cancellationToken);

    public Task RestoreByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Set.Where(entity => entity.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.IsActive, true), cancellationToken);

    public async Task HardDeleteByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
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
