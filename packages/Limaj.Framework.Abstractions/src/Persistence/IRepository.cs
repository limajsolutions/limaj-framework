using Limaj.Framework.Abstractions.Domain;
using System.Linq.Expressions;

namespace Limaj.Framework.Abstractions.Contracts;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task InsertAsync(T entity, CancellationToken cancellationToken);
    Task UpdateAsync(T entity, CancellationToken cancellationToken);
    Task SoftDeleteByIdAsync(Guid id, CancellationToken cancellationToken);
    Task RestoreByIdAsync(Guid id, CancellationToken cancellationToken);
    Task HardDeleteByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken, bool includeInactive = false);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken, bool includeInactive = false);
    IQueryable<T> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken, bool includeInactive = false);
    Task SaveAsync(CancellationToken cancellationToken);
    IQueryable<T> GetAllAsync();
}
