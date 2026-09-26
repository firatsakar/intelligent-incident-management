using Microsoft.EntityFrameworkCore.ChangeTracking;
using IncidentService.Domain.ValueObjects;
using System.Text.Json;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentService.Infrastructure.Persistence.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    /// <summary>
    /// At most one open incident per external id in an organisation. The repository recognises a
    /// violation of it by this name.
    /// </summary>
    public const string OpenExternalIdIndex = "IX_incidents_open_external_id";

    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();

        // Every list and every stat reads by organisation first, so it leads the composite; the
        // existing single-column indexes stay for the rest of each predicate.
        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAt });

        builder.Property(x => x.Title).IsRequired().HasMaxLength(IncidentConstants.TitleMaxLength);

        builder
            .Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(IncidentConstants.DescriptionMaxLength);

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(64);

        builder.Property(x => x.Priority).IsRequired().HasConversion<string>().HasMaxLength(64);

        builder.Property(x => x.Source).IsRequired().HasConversion<string>().HasMaxLength(64);

        builder.Property(x => x.AssignedTeam).HasMaxLength(IncidentConstants.TeamMaxLength);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property(x => x.DetectedAt);

        builder.Property(x => x.UpdatedAt);

        builder
            .Property(x => x.AiSuggestedCategory)
            .HasMaxLength(IncidentConstants.AiCategoryMaxLength);

        builder.Property(x => x.AiReasoning).HasMaxLength(IncidentConstants.AiReasoningMaxLength);

        builder.Property(x => x.IsAiAnalyzed).IsRequired().HasDefaultValue(false);

        builder.Property(x => x.AiConfidence);

        builder.Ignore(x => x.AiRelatedChanges);

        builder
            .Property<List<AiRelatedChange>>("_aiRelatedChanges")
            .HasColumnName("ai_related_changes")
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<AiRelatedChange>>(json, (JsonSerializerOptions?)null) ?? new()
            )
            .Metadata.SetValueComparer(
                new ValueComparer<List<AiRelatedChange>>(
                    (a, b) => a!.SequenceEqual(b!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    value => value.ToList()
                )
            );

        builder.Property(x => x.Verdict).HasConversion<string>().HasMaxLength(32);

        builder.Property(x => x.ResolvedAt);

        builder.Property(x => x.ExternalId).HasMaxLength(IncidentConstants.ExternalIdMaxLength);

        builder.Property(x => x.ReportedBy).HasMaxLength(IncidentApiKey.NameMaxLength);

        // Adım 27: the same external id finds the open incident instead of opening a second one.
        // The check in the handler answers the common case; this is what holds when two requests
        // carrying the same alert arrive together. Partial: resolved incidents drop out of it, so
        // an alert that fires again after one was resolved opens a new incident.
        builder
            .HasIndex(x => new { x.OrganizationId, x.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL AND \"Status\" IN ('Open', 'InProgress')")
            .HasDatabaseName(OpenExternalIdIndex);

        builder.Ignore(x => x.DomainEvents);
    }
}
