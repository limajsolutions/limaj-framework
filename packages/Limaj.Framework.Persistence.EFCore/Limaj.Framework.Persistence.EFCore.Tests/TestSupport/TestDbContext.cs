using Microsoft.EntityFrameworkCore;

namespace Limaj.Framework.Persistence.EFCore.Tests.TestSupport;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();
}

public class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
