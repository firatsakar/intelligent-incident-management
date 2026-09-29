using IncidentService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentService.Infrastructure.Persistence.Configurations;

public sealed class IncidentApiKeyConfiguration : IEntityTypeConfiguration<IncidentApiKey>
{
    public void Configure(EntityTypeBuilder<IncidentApiKey> builder)
    {
        builder.ToTable("incident_api_keys");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(IncidentApiKey.NameMaxLength);

        // The handler refuses a second key with the same name; this is what holds when two
        // Admins try at once.
        builder.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();

        // Every intake request finds its key by this, across organisations.
        builder.Property(x => x.KeyHash).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.KeyHash).IsUnique();

        builder.Property(x => x.KeyPrefix).IsRequired().HasMaxLength(32);

        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property(x => x.LastUsedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
