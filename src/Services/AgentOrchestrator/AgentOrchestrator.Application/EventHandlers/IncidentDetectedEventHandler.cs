using AgentOrchestrator.Application.Commands.AnalyzeIncident;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Application.EventHandlers;

public sealed class IncidentDetectedEventHandler : IIntegrationEventHandler<IncidentDetectedEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<IncidentDetectedEventHandler> _logger;

    public IncidentDetectedEventHandler(
        ISender sender,
        ILogger<IncidentDetectedEventHandler> logger
    )
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task HandleAsync(
        IncidentDetectedEvent integrationEvent,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "IncidentDetectedEvent received for incident {IncidentId}. Starting AI analysis...",
            integrationEvent.IncidentId
        );

        var command = new AnalyzeIncidentCommand
        {
            IncidentId = integrationEvent.IncidentId,
            Title = integrationEvent.Title,
            Description = integrationEvent.Description,
        };

        await _sender.Send(command, cancellationToken);

        _logger.LogInformation(
            "AI analysis triggered for incident {IncidentId}.",
            integrationEvent.IncidentId
        );
    }
}
