using Limaj.Framework.Abstractions.Domain;
using Limaj.Framework.Persistence.EFCore.Persistence.Configurations;
using Limaj.Framework.Persistence.EFCore.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limaj.Framework.Persistence.EFCore.Tests.TestSupport;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();
    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TestEntityConfiguration());
        modelBuilder.UseUtcDateTimeConversion();
    }
}

public class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TestEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}

public class TestEntityConfiguration : BaseEntityConfiguration<TestEntity>
{
    public override void Configure(EntityTypeBuilder<TestEntity> builder)
    {
        base.Configure(builder);
        builder.Property(entity => entity.Name).IsRequired();
    }
}
