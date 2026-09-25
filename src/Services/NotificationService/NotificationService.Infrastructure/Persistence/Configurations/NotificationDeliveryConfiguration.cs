using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public sealed class NotificationDeliveryConfiguration
    : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();

        // The stats query reads a window of one organisation's deliveries.
        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAt });

        builder.Property(x => x.IntegrationId).IsRequired();
        builder.Property(x => x.IntegrationName).HasMaxLength(128);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.IncidentId).IsRequired();

        builder.Property(x => x.EventId).IsRequired();

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.AttemptCount).IsRequired();

        builder.Property(x => x.LastError).HasMaxLength(2048);

        builder.Property(x => x.SentAt);

        // The idempotency key. At-least-once delivery means the same event can arrive twice; the
        // unique constraint is the last line of defence behind the explicit existence check.
        builder.HasIndex(x => new { x.IntegrationId, x.IncidentId }).IsUnique();

        // Delivery history for one incident — used by the audit endpoint.
        builder.HasIndex(x => x.IncidentId);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
