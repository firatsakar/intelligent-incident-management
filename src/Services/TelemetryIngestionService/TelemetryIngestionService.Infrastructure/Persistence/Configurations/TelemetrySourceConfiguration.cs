using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Configurations;

public sealed class TelemetrySourceConfiguration : IEntityTypeConfiguration<TelemetrySource>
{
    public void Configure(EntityTypeBuilder<TelemetrySource> builder)
    {
        builder.ToTable("telemetry_sources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.PollIntervalSeconds).IsRequired();

        builder.Ignore(x => x.Config);
        builder.Property<Dictionary<string, string>>("_config").AsJsonb("config");

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
