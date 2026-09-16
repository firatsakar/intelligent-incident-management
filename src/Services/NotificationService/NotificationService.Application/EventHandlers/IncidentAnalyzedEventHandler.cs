using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using MediatR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Commands.DispatchNotifications;

namespace NotificationService.Application.EventHandlers;

public sealed class IncidentAnalyzedEventHandler : IIntegrationEventHandler<IncidentAnalyzedEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<IncidentAnalyzedEventHandler> _logger;

    public IncidentAnalyzedEventHandler(
        ISender sender,
        ILogger<IncidentAnalyzedEventHandler> logger
    )
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task HandleAsync(
        IncidentAnalyzedEvent integrationEvent,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "IncidentAnalyzedEvent received for incident {IncidentId}. Dispatching notifications...",
            integrationEvent.IncidentId
        );

        var command = new DispatchNotificationsCommand
        {
            EventId = integrationEvent.Id,
            IncidentId = integrationEvent.IncidentId,
            IncidentTitle = integrationEvent.IncidentTitle,
            SuggestedPriority = integrationEvent.SuggestedPriority,
            SuggestedCategory = integrationEvent.SuggestedCategory,
            Reasoning = integrationEvent.Reasoning,
            Confidence = integrationEvent.Confidence,
            AnalyzedAt = integrationEvent.OccurredAt,
        };

        await _sender.Send(command, cancellationToken);
    }
}
