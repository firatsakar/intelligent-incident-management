using MediatR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Commands.DispatchNotifications;

public sealed class DispatchNotificationsCommandHandler : IRequestHandler<DispatchNotificationsCommand>
{
    private readonly IIntegrationRepository _integrations;
    private readonly INotificationDeliveryRepository _deliveries;
    private readonly INotificationChannelResolver _channels;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<DispatchNotificationsCommandHandler> _logger;

    public DispatchNotificationsCommandHandler(
        IIntegrationRepository integrations,
        INotificationDeliveryRepository deliveries,
        INotificationChannelResolver channels,
        IRealtimeNotifier realtime,
        ILogger<DispatchNotificationsCommandHandler> logger
    )
    {
        _integrations = integrations;
        _deliveries = deliveries;
        _channels = channels;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task Handle(
        DispatchNotificationsCommand request,
        CancellationToken cancellationToken
    )
    {
        var integrations = await _integrations.GetEnabledAsync(cancellationToken);

        if (integrations.Count == 0)
        {
            _logger.LogInformation(
                "No enabled integrations; nothing to notify for incident {IncidentId}.",
                request.IncidentId
            );

            return;
        }

        var priority = ParsePriority(request.SuggestedPriority, request.IncidentId);
        var message = BuildMessage(request);
        var recorded = new List<NotificationDelivery>();

        foreach (var integration in integrations)
        {
            if (!integration.Matches(priority, request.SuggestedCategory))
                continue;

            if (await _deliveries.ExistsAsync(integration.Id, request.IncidentId, cancellationToken))
            {
                _logger.LogInformation(
                    "Incident {IncidentId} was already notified through integration {IntegrationName}; skipping redelivery.",
                    request.IncidentId,
                    integration.Name
                );

                continue;
            }

            var delivery = await SendAsync(message, integration, request, cancellationToken);

            await _deliveries.AddAsync(delivery, cancellationToken);

            recorded.Add(delivery);
        }

        var dispatched = recorded.Count;

        try
        {
            await _deliveries.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // The notifications have already gone out. Rethrowing would dead-letter the message
            // and a later redelivery would send them all again, so the honest outcome is to log
            // that the audit rows were lost rather than to duplicate real notifications.
            _logger.LogError(
                ex,
                "Failed to persist delivery records for incident {IncidentId}. Notifications were already sent.",
                request.IncidentId
            );

            // Nothing was stored, so nothing is announced: a strip of delivery rows that a
            // refresh erases is worse than one that arrives late.
            recorded.Clear();
        }

        foreach (var delivery in recorded)
        {
            await _realtime.DeliveryRecordedAsync(
                NotificationDeliveryDto.FromDomain(delivery),
                cancellationToken
            );
        }

        _logger.LogInformation(
            "Dispatched {DispatchedCount} notification(s) for incident {IncidentId} across {EnabledCount} enabled integration(s).",
            dispatched,
            request.IncidentId,
            integrations.Count
        );
    }

    private async Task<NotificationDelivery> SendAsync(
        NotificationMessage message,
        Integration integration,
        DispatchNotificationsCommand request,
        CancellationToken cancellationToken
    )
    {
        var delivery = NotificationDelivery.Start(
            integration.Id,
            request.IncidentId,
            request.EventId
        );

        try
        {
            var channel = _channels.Resolve(integration.Channel);
            var result = await channel.SendAsync(message, integration, cancellationToken);

            if (result.IsSuccess)
                delivery.MarkSent();
            else
                delivery.MarkFailed(result.Error ?? "The channel reported a failure without a reason.");
        }
        catch (Exception ex)
        {
            // Isolated per integration on purpose: one broken channel must not stop the others,
            // and must not bubble up and get the message dead-lettered after other channels
            // have already delivered it.
            _logger.LogError(
                ex,
                "Channel {Channel} threw while notifying incident {IncidentId} through integration {IntegrationName}.",
                integration.Channel,
                request.IncidentId,
                integration.Name
            );

            delivery.MarkFailed(ex.Message);
        }

        return delivery;
    }

    private static NotificationMessage BuildMessage(DispatchNotificationsCommand request)
    {
        return new NotificationMessage
        {
            IncidentId = request.IncidentId,
            IncidentTitle = request.IncidentTitle,
            SuggestedPriority = request.SuggestedPriority,
            SuggestedCategory = request.SuggestedCategory,
            Reasoning = request.Reasoning,
            Confidence = request.Confidence,
            AnalyzedAt = request.AnalyzedAt,
        };
    }

    private IncidentPriority? ParsePriority(string suggestedPriority, Guid incidentId)
    {
        if (Enum.TryParse<IncidentPriority>(suggestedPriority, ignoreCase: true, out var parsed))
            return parsed;

        _logger.LogWarning(
            "Unrecognised priority '{SuggestedPriority}' on incident {IncidentId}; priority filters will be ignored for it.",
            suggestedPriority,
            incidentId
        );

        return null;
    }
}
