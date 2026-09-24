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
        // Unique within an organisation. Every organisation's first rule is called the same thing —
        // DefaultDetectionRule.Name — so a table-wide index made the second organisation's rule a
        // duplicate key, the consumer that writes it failed, the message was dead-lettered, and
        // the organisation detected nothing with no error anywhere a person would see. Found by
        // the acceptance run, not by a test: a unit test with a mocked repository has no index.
        builder.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();

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
