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

    // Every delivery attempted in a window. The rows are narrow — no payload is stored, only the
    // outcome — so this is counted from the rows rather than aggregated in SQL, which keeps the
    // median and the last error in one pass.
    Task<IReadOnlyList<NotificationDelivery>> GetWindowAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
