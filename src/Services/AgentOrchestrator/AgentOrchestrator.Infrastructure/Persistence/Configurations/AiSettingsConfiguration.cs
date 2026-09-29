using AgentOrchestrator.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentOrchestrator.Infrastructure.Persistence.Configurations;

public sealed class AiSettingsConfiguration : IEntityTypeConfiguration<AiSettings>
{
    public void Configure(EntityTypeBuilder<AiSettings> builder)
    {
        builder.ToTable("ai_settings");

        builder.HasKey(x => x.Id);

        // One row per organisation: the save handler updates it rather than adding a second.
        builder.HasIndex(x => x.OrganizationId).IsUnique();

        builder.Property(x => x.ResponseLanguage).IsRequired().HasConversion<string>().HasMaxLength(32);

        builder.Ignore(x => x.DomainEvents);
    }
}
