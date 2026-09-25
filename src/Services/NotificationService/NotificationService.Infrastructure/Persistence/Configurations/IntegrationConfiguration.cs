using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public sealed class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("integrations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);

        builder.Property(x => x.OrganizationId).IsRequired();

        // Unique within an organisation rather than across the table. Two teams can each have an
        // integration called "On-call email"; one team cannot have two, because the name is how a
        // person tells them apart on the settings screen.
        builder.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();

        builder.Property(x => x.Channel).IsRequired().HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.IsEnabled).IsRequired();

        builder.Property(x => x.MinPriority).HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.CategoryFilter).HasMaxLength(128);

        // The settings bag is written through the backing field, since the aggregate exposes it
        // as a read-only dictionary. Stored as jsonb because the shape differs per channel.
        builder.Ignore(x => x.Config);

        var config = builder
            .Property<Dictionary<string, string>>("_config")
            .HasColumnName("config")
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                json =>
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        json,
                        (JsonSerializerOptions?)null
                    ) ?? new Dictionary<string, string>()
            )
            .IsRequired();

        // Without a comparer EF treats the mutable dictionary by reference and misses edits.
        config.Metadata.SetValueComparer(
            new ValueComparer<Dictionary<string, string>>(
                (left, right) =>
                    left != null && right != null && left.Count == right.Count && !left.Except(right).Any(),
                value =>
                    value.Aggregate(
                        0,
                        (hash, pair) => HashCode.Combine(hash, pair.Key.GetHashCode(), pair.Value.GetHashCode())
                    ),
                value => new Dictionary<string, string>(value)
            )
        );

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
