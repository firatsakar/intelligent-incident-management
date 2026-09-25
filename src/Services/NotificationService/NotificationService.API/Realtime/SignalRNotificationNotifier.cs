using BuildingBlocks.Web;
using BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;

namespace NotificationService.API.Realtime;

public sealed class SignalRNotificationNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationNotifier> _logger;
    private readonly IOrganizationContext _organization;

    public SignalRNotificationNotifier(
        IHubContext<NotificationHub> hub,
        ILogger<SignalRNotificationNotifier> logger,
        IOrganizationContext organization
    )
    {
        _hub = hub;
        _logger = logger;
        _organization = organization;
    }

    public async Task DeliveryRecordedAsync(
        NotificationDeliveryDto delivery,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await Audience().SendAsync("deliveryRecorded", delivery, cancellationToken);
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
    ) => SendAsync("integrationChanged", integration, integration.Id, cancellationToken, adminsOnly: true);

    public Task IntegrationDeletedAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default
    ) => SendAsync("integrationDeleted", integrationId, integrationId, cancellationToken, adminsOnly: true);

    /// <summary>
    /// Same contract as the delivery broadcast above: the write is already committed, so a
    /// failure here is logged and swallowed. Throwing would turn a successful command into a 500
    /// over a screen refresh that the next read would have fixed anyway.
    /// </summary>
    private async Task SendAsync(
        string message,
        object payload,
        Guid id,
        CancellationToken cancellationToken,
        bool adminsOnly = false
    )
    {
        try
        {
            await Audience(adminsOnly).SendAsync(message, payload, cancellationToken);
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

    // The organisation in scope is the organisation whose row was just written: the same scope
    // stamped it and the same query filter would read it back. With no scope there is no audience
    // at all — never Clients.All, which is what every one of these used to be and is exactly the
    // leak this exists to close. The send then goes nowhere, and the caller swallows failures
    // anyway because a broadcast is the least important thing a command does.
    //
    // Configuration changes go to the organisation's Admins only: only they may read it at all.
    private IClientProxy Audience(bool adminsOnly = false)
    {
        var organizationId = _organization.OrganizationId;

        if (organizationId is null)
        {
            _logger.LogWarning("Realtime push skipped: no organisation in scope to address it to.");

            return NoAudience.Instance;
        }

        return _hub.Clients.Group(
            adminsOnly
                ? OrganizationGroups.AdminsOf(organizationId.Value)
                : OrganizationGroups.For(organizationId.Value)
        );
    }

    /// <summary>A proxy that sends to nobody, for the case where there is nobody it may send to.</summary>
    private sealed class NoAudience : IClientProxy
    {
        public static readonly NoAudience Instance = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
