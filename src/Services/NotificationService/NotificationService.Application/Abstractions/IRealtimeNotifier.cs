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
}
