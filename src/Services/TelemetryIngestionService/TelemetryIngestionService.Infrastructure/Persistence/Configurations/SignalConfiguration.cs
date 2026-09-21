using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Configurations;

public sealed class SignalConfiguration : IEntityTypeConfiguration<Signal>
{
    public void Configure(EntityTypeBuilder<Signal> builder)
    {
        builder.ToTable("signals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ErrorSignatureId).IsRequired();
        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.DetectedAt).IsRequired();
        builder.Property(x => x.WindowStart).IsRequired();
        builder.Property(x => x.WindowEnd).IsRequired();
        builder.Property(x => x.OccurrenceCount).IsRequired();
        builder.Property(x => x.Confidence).IsRequired();

        builder.Ignore(x => x.ScoreBreakdown);
        builder.Property<Dictionary<string, double>>("_scoreBreakdown").AsJsonb("score_breakdown");

        builder.Property(x => x.Reason).HasMaxLength(1024);
        builder.Property(x => x.IncidentId);

        builder.HasIndex(x => x.ErrorSignatureId);
        builder.HasIndex(x => x.DetectedAt).HasMethod("brin");

        // The weak-signal queue is read by status.
        builder.HasIndex(x => x.Status);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
