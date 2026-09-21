using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using IncidentService.Application.Commands.CreateIncidentFromSignal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IncidentService.Application.EventHandlers;

public sealed class SignalPromotedEventHandler : IIntegrationEventHandler<SignalPromotedEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<SignalPromotedEventHandler> _logger;

    public SignalPromotedEventHandler(ISender sender, ILogger<SignalPromotedEventHandler> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task HandleAsync(
        SignalPromotedEvent integrationEvent,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "SignalPromotedEvent received for {Service} (confidence {Confidence:F2}). Opening incident {IncidentId}...",
            integrationEvent.Service,
            integrationEvent.Confidence,
            integrationEvent.IncidentId
        );

        var command = new CreateIncidentFromSignalCommand
        {
            IncidentId = integrationEvent.IncidentId,
            Title = integrationEvent.Title,
            Description = integrationEvent.Description,
            Severity = integrationEvent.Severity,
            DetectedAt = integrationEvent.DetectedAt,
        };

        await _sender.Send(command, cancellationToken);
    }
}
