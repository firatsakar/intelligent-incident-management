using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Configurations;

public sealed class ErrorSignatureConfiguration : IEntityTypeConfiguration<ErrorSignature>
{
    public void Configure(EntityTypeBuilder<ErrorSignature> builder)
    {
        builder.ToTable("error_signatures");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();
        builder.HasIndex(x => x.OrganizationId);

        // The identity of a distinct error. Unique so concurrent pollers cannot create two rows
        // for the same signature.
        builder.Property(x => x.Fingerprint).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.Fingerprint).IsUnique();

        builder.Property(x => x.Service).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ExceptionType).HasMaxLength(512);
        builder.Property(x => x.NormalizedMessage).IsRequired().HasMaxLength(4096);

        builder.Property(x => x.FirstSeenAt).IsRequired();
        builder.Property(x => x.LastSeenAt).IsRequired();
        builder.Property(x => x.OccurrenceCount).IsRequired();

        builder.Property(x => x.CurrentIncidentId);
        builder.Property(x => x.LastPromotedAt);
        builder.Property(x => x.CurrentIncidentOccurrences).IsRequired();

        builder.Property(x => x.IsMuted).IsRequired();

        builder.Property(x => x.PromotionCount).IsRequired();
        builder.Property(x => x.ConfirmedRealCount).IsRequired();
        builder.Property(x => x.FalsePositiveCount).IsRequired();

        // Resolving an incident has to find its signature by incident id.
        builder.HasIndex(x => x.CurrentIncidentId);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
