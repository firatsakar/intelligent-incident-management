using System.Text.Json;
using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentOrchestrator.Infrastructure.Persistence.Configurations;

public sealed class GitHubConnectionConfiguration : IEntityTypeConfiguration<GitHubConnection>
{
    public void Configure(EntityTypeBuilder<GitHubConnection> builder)
    {
        builder.ToTable("github_connections");

        builder.HasKey(x => x.Id);

        // One per organisation: the settings screen edits "the" connection, not one of several.
        builder.HasIndex(x => x.OrganizationId).IsUnique();

        // Stored as given. At-rest encryption is the same open item as the notification
        // integrations' credentials (production_necessaries.MD §1), not a second design.
        builder.Property(x => x.Token).IsRequired().HasMaxLength(512);

        builder.Property(x => x.IsEnabled).IsRequired();

        builder.Ignore(x => x.Repositories);

        builder
            .Property<List<RepositoryMapping>>("_repositories")
            .HasColumnName("repositories")
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<RepositoryMapping>>(json, (JsonSerializerOptions?)null) ?? new()
            )
            .Metadata.SetValueComparer(
                new ValueComparer<List<RepositoryMapping>>(
                    (a, b) => a!.SequenceEqual(b!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    value => value.ToList()
                )
            );

        builder.Ignore(x => x.DomainEvents);
    }
}
