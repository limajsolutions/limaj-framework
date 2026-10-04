using Limaj.Framework.Abstractions.Domain;
using Xunit;

namespace Limaj.Framework.Abstractions.Tests;

/// <summary>
/// Soft-delete contract of <see cref="BaseEntity"/>: a new entity is active, so it shows up in
/// BaseRepository's default (IsActive-filtered) reads until it is soft-deleted.
/// </summary>
public class BaseEntityTests
{
    private sealed class SampleEntity : BaseEntity;

    [Fact]
    public void NewEntity_IsActiveByDefault()
    {
        var entity = new SampleEntity();

        Assert.True(entity.IsActive);
    }
}
