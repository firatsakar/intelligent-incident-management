using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using IncidentService.Application.Commands.RecordAiAnalysisFailure;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IncidentService.Application.EventHandlers;

public sealed class IncidentAnalysisFailedEventHandler
    : IIntegrationEventHandler<IncidentAnalysisFailedEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<IncidentAnalysisFailedEventHandler> _logger;

    public IncidentAnalysisFailedEventHandler(
        ISender sender,
        ILogger<IncidentAnalysisFailedEventHandler> logger
    )
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task HandleAsync(
        IncidentAnalysisFailedEvent integrationEvent,
        CancellationToken cancellationToken = default
    )
    {
        // Warning rather than information: the incident is real and is now missing the
        // enrichment the rest of the chain assumes it has. Notifications still go out — a failed
        // analysis is a reason to tell somebody sooner, not later.
        _logger.LogWarning(
            "IncidentAnalysisFailedEvent received for incident {IncidentId}: {Error}",
            integrationEvent.IncidentId,
            integrationEvent.Error
        );

        await _sender.Send(
            new RecordAiAnalysisFailureCommand
            {
                IncidentId = integrationEvent.IncidentId,
                Error = integrationEvent.Error,
            },
            cancellationToken
        );
    }
}
