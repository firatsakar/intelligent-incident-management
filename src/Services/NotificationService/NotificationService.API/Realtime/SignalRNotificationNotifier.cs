using Microsoft.AspNetCore.SignalR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;

namespace NotificationService.API.Realtime;

public sealed class SignalRNotificationNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationNotifier> _logger;

    public SignalRNotificationNotifier(
        IHubContext<NotificationHub> hub,
        ILogger<SignalRNotificationNotifier> logger
    )
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task DeliveryRecordedAsync(
        NotificationDeliveryDto delivery,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await _hub.Clients.All.SendAsync("deliveryRecorded", delivery, cancellationToken);
        }
        catch (Exception ex)
        {
            // The notification itself already went out and the row is already committed. A failed
            // broadcast must not bubble up and dead-letter the message, which would resend every
            // notification in the fan-out on redelivery.
            _logger.LogWarning(
                ex,
                "Failed to broadcast delivery {DeliveryId} over the notification hub.",
                delivery.Id
            );
        }
    }
}
