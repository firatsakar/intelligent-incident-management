using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Configurations;

public sealed class SourceCursorConfiguration : IEntityTypeConfiguration<SourceCursor>
{
    public void Configure(EntityTypeBuilder<SourceCursor> builder)
    {
        builder.ToTable("source_cursors");

        builder.HasKey(x => x.Id);

        // Exactly one cursor per source.
        builder.Property(x => x.TelemetrySourceId).IsRequired();
        builder.HasIndex(x => x.TelemetrySourceId).IsUnique();

        builder.Property(x => x.Position).HasMaxLength(512);
        builder.Property(x => x.LastEventTimestamp);
        builder.Property(x => x.LastPolledAt);
        builder.Property(x => x.LastError).HasMaxLength(2048);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
