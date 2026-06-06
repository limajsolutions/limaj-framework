using Limaj.Framework.Abstractions.Contracts;
using Limaj.Framework.Abstractions.Domain;

namespace Limaj.Framework.Application.Services;

public abstract class BaseService<TEntity>(IRepository<TEntity> repository)
    where TEntity : BaseEntity
{
    protected IRepository<TEntity> Repository { get; } = repository;

    protected Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Repository.GetByIdAsync(id, cancellationToken);

    protected Task InsertAsync(TEntity entity, CancellationToken cancellationToken) =>
        Repository.InsertAsync(entity, cancellationToken);

    protected Task UpdateAsync(TEntity entity, CancellationToken cancellationToken) =>
        Repository.UpdateAsync(entity, cancellationToken);

    protected Task SaveAsync(CancellationToken cancellationToken) =>
        Repository.SaveAsync(cancellationToken);
}
