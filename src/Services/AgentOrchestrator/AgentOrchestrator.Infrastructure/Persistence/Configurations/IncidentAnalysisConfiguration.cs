using AgentOrchestrator.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentOrchestrator.Infrastructure.Persistence.Configurations;

public sealed class IncidentAnalysisConfiguration : IEntityTypeConfiguration<IncidentAnalysis>
{
    public void Configure(EntityTypeBuilder<IncidentAnalysis> builder)
    {
        builder.ToTable("incident_analyses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IncidentId).IsRequired();

        builder.HasIndex(x => x.IncidentId);

        builder.Property(x => x.IncidentTitle).IsRequired().HasMaxLength(256);

        builder.Property(x => x.IncidentDescription).IsRequired().HasMaxLength(4096);

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.ErrorMessage).HasMaxLength(2048);

        builder.OwnsOne(
            x => x.Result,
            resultBuilder =>
            {
                resultBuilder.ToJson("result");
            }
        );

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
