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

    public Task IntegrationChangedAsync(
        IntegrationDto integration,
        CancellationToken cancellationToken = default
    ) => SendAsync("integrationChanged", integration, integration.Id, cancellationToken);

    public Task IntegrationDeletedAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default
    ) => SendAsync("integrationDeleted", integrationId, integrationId, cancellationToken);

    /// <summary>
    /// Same contract as the delivery broadcast above: the write is already committed, so a
    /// failure here is logged and swallowed. Throwing would turn a successful command into a 500
    /// over a screen refresh that the next read would have fixed anyway.
    /// </summary>
    private async Task SendAsync(
        string message,
        object payload,
        Guid id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _hub.Clients.All.SendAsync(message, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to broadcast {Message} for {IntegrationId} over the notification hub.",
                message,
                id
            );
        }
    }
}
