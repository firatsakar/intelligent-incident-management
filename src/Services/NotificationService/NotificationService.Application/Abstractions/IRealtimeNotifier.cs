using NotificationService.Application.DTOs;

namespace NotificationService.Application.Abstractions;

// The delivery row is what the incident detail screen shows as the notification strip, and it is
// the last link in the chain a demo watches: channels turning green one by one. Carries the whole
// DTO for the same reason as the incident hub — pushing an id would cost a request per delivery,
// and a fan-out across several integrations produces several at once.
public interface IRealtimeNotifier
{
    Task DeliveryRecordedAsync(
        NotificationDeliveryDto delivery,
        CancellationToken cancellationToken = default
    );

    // Configuration changes, so a second operator — or the same one in a second tab — is not
    // looking at a channel that was renamed, disabled or deleted underneath them. The DTO is the
    // masked one the HTTP read already returns, so nothing travels here that a GET would not.
    Task IntegrationChangedAsync(
        IntegrationDto integration,
        CancellationToken cancellationToken = default
    );

    // Only the id survives a delete, which is why this is a separate message rather than a
    // changed-with-a-flag.
    Task IntegrationDeletedAsync(Guid integrationId, CancellationToken cancellationToken = default);
}
