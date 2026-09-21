using NotificationService.Domain.Aggregates;

namespace NotificationService.Application.Abstractions;

public interface INotificationDeliveryRepository
{
    // The idempotency check: a row for this (integration, incident) pair means the notification
    // already went out and a redelivered event must not send another one.
    Task<bool> ExistsAsync(
        Guid integrationId,
        Guid incidentId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<NotificationDelivery>> GetByIncidentIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
