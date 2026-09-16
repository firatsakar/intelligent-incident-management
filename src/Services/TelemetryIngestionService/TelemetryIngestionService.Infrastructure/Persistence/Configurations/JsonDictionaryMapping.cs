using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TelemetryIngestionService.Infrastructure.Persistence.Configurations;

// Two aggregates here keep a dictionary in jsonb through a private backing field. Both need the
// same converter plus a comparer — without the comparer EF compares the mutable dictionary by
// reference and silently misses edits.
internal static class JsonDictionaryMapping
{
    public static PropertyBuilder<Dictionary<string, TValue>> AsJsonb<TValue>(
        this PropertyBuilder<Dictionary<string, TValue>> builder,
        string columnName
    )
        where TValue : notnull
    {
        builder
            .HasColumnName(columnName)
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<Dictionary<string, TValue>, string>(
                    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    json =>
                        JsonSerializer.Deserialize<Dictionary<string, TValue>>(
                            json,
                            (JsonSerializerOptions?)null
                        ) ?? new Dictionary<string, TValue>()
                )
            )
            .IsRequired();

        builder.Metadata.SetValueComparer(
            new ValueComparer<Dictionary<string, TValue>>(
                (left, right) =>
                    left != null
                    && right != null
                    && left.Count == right.Count
                    && !left.Except(right).Any(),
                value =>
                    value.Aggregate(
                        0,
                        (hash, pair) => HashCode.Combine(hash, pair.Key.GetHashCode(), pair.Value.GetHashCode())
                    ),
                value => new Dictionary<string, TValue>(value)
            )
        );

        return builder;
    }
}
