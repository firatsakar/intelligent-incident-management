using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Configurations;

public sealed class DetectionRuleConfiguration : IEntityTypeConfiguration<DetectionRule>
{
    public void Configure(EntityTypeBuilder<DetectionRule> builder)
    {
        builder.ToTable("detection_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();
        builder.HasIndex(x => x.OrganizationId);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => x.Name).IsUnique();

        // Null means the catch-all rule.
        builder.Property(x => x.Service).HasMaxLength(128);

        builder.Property(x => x.MinSeverity).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.WindowSeconds).IsRequired();
        builder.Property(x => x.Threshold).IsRequired();
        builder.Property(x => x.DedupWindowHours).IsRequired();
        builder.Property(x => x.PromoteThreshold).IsRequired();
        builder.Property(x => x.IsEnabled).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
