using Limaj.Framework.Abstractions.Contracts;
using Limaj.Framework.Abstractions.Domain;
using Limaj.Framework.Application.Services;
using Moq;
using Xunit;

namespace Limaj.Framework.Application.Tests;

public class BaseServiceTests
{
    public class TestEntity : BaseEntity;

    private class TestService(IRepository<TestEntity> repository) : BaseService<TestEntity>(repository)
    {
        public Task<TestEntity?> ExposeGetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            GetByIdAsync(id, cancellationToken);

        public Task ExposeInsertAsync(TestEntity entity, CancellationToken cancellationToken) =>
            InsertAsync(entity, cancellationToken);

        public Task ExposeUpdateAsync(TestEntity entity, CancellationToken cancellationToken) =>
            UpdateAsync(entity, cancellationToken);

        public Task ExposeSaveAsync(CancellationToken cancellationToken) =>
            SaveAsync(cancellationToken);
    }

    [Fact]
    public async Task GetByIdAsync_DelegatesToRepository()
    {
        var entity = new TestEntity { Id = Guid.NewGuid() };
        var repository = new Mock<IRepository<TestEntity>>();
        repository.Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var service = new TestService(repository.Object);

        var result = await service.ExposeGetByIdAsync(entity.Id, CancellationToken.None);

        Assert.Same(entity, result);
        repository.Verify(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InsertAsync_DelegatesToRepository()
    {
        var entity = new TestEntity { Id = Guid.NewGuid() };
        var repository = new Mock<IRepository<TestEntity>>();
        var service = new TestService(repository.Object);

        await service.ExposeInsertAsync(entity, CancellationToken.None);

        repository.Verify(r => r.InsertAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DelegatesToRepository()
    {
        var entity = new TestEntity { Id = Guid.NewGuid() };
        var repository = new Mock<IRepository<TestEntity>>();
        var service = new TestService(repository.Object);

        await service.ExposeUpdateAsync(entity, CancellationToken.None);

        repository.Verify(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_DelegatesToRepository()
    {
        var repository = new Mock<IRepository<TestEntity>>();
        var service = new TestService(repository.Object);

        await service.ExposeSaveAsync(CancellationToken.None);

        repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
