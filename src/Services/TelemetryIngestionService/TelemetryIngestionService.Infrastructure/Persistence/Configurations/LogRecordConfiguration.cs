using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Configurations;

public sealed class LogRecordConfiguration : IEntityTypeConfiguration<LogRecord>
{
    public void Configure(EntityTypeBuilder<LogRecord> builder)
    {
        builder.ToTable("log_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();
        builder.HasIndex(x => x.OrganizationId);

        builder.Property(x => x.TelemetrySourceId).IsRequired();
        builder.Property(x => x.SourceEventId).HasMaxLength(128);
        builder.Property(x => x.Service).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Severity).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(4096);
        builder.Property(x => x.NormalizedMessage).IsRequired().HasMaxLength(4096);
        builder.Property(x => x.ExceptionType).HasMaxLength(512);
        builder.Property(x => x.StackTrace).HasMaxLength(8192);
        builder.Property(x => x.Fingerprint).HasMaxLength(64);
        builder.Property(x => x.Timestamp).IsRequired();
        builder.Property(x => x.IngestedAt).IsRequired();
        builder.Property(x => x.HasClockSkew).IsRequired();

        // Every query against this table is a time window, and the table is append-only in
        // timestamp order — which is exactly the shape BRIN is built for, at a fraction of the
        // size of a btree.
        builder.HasIndex(x => x.Timestamp).HasMethod("brin");

        // Fingerprint plus time answers "how often did this signature fire in this window", the
        // question burst detection asks constantly.
        // Organisation first, because every one of these queries now carries it — the filter adds
        // it whether the caller wrote it or not.
        builder.HasIndex(x => new { x.OrganizationId, x.Fingerprint, x.Timestamp });

        // Guards against a replayed batch inserting the same event twice.
        builder
            .HasIndex(x => new { x.TelemetrySourceId, x.SourceEventId })
            .IsUnique()
            .HasFilter("\"SourceEventId\" IS NOT NULL");

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
