using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Limaj.Framework.Persistence.EFCore.Persistence.Extensions;

/// <summary>
/// Forces every DateTime/DateTimeOffset property in the model to round-trip as UTC, regardless
/// of what Kind the provider stored it with. Call from DbContext.OnModelCreating after
/// configuring entities.
/// </summary>
public static class UtcDateTimeConversionExtensions
{
    private static readonly ValueConverter<DateTime, DateTime> DateTimeConverter = new(
        toProvider => DateTime.SpecifyKind(toProvider, DateTimeKind.Utc),
        fromProvider => DateTime.SpecifyKind(fromProvider, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeConverter = new(
        toProvider => toProvider.HasValue ? DateTime.SpecifyKind(toProvider.Value, DateTimeKind.Utc) : toProvider,
        fromProvider => fromProvider.HasValue ? DateTime.SpecifyKind(fromProvider.Value, DateTimeKind.Utc) : fromProvider);

    private static readonly ValueConverter<DateTimeOffset, DateTimeOffset> DateTimeOffsetConverter = new(
        toProvider => toProvider.ToUniversalTime(),
        fromProvider => fromProvider.ToUniversalTime());

    private static readonly ValueConverter<DateTimeOffset?, DateTimeOffset?> NullableDateTimeOffsetConverter = new(
        toProvider => toProvider.HasValue ? toProvider.Value.ToUniversalTime() : toProvider,
        fromProvider => fromProvider.HasValue ? fromProvider.Value.ToUniversalTime() : fromProvider);

    public static void UseUtcDateTimeConversion(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(DateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(NullableDateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableDateTimeOffsetConverter);
                }
            }
        }
    }
}
