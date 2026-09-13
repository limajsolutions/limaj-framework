using Limaj.Framework.Persistence.EFCore.Repositories.Base;

namespace Limaj.Framework.Persistence.EFCore.Tests.TestSupport;

public class TestEntityRepository(TestDbContext dbContext) : BaseRepository<TestEntity, TestDbContext>(dbContext);
