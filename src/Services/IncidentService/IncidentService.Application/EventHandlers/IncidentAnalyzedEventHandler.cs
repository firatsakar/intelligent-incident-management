using IncidentService.Domain.ValueObjects;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using IncidentService.Application.Commands.ApplyAiAnalysis;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IncidentService.Application.EventHandlers;

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
            "IncidentAnalyzedEvent received for incident {IncidentId}. Applying AI analysis...",
            integrationEvent.IncidentId
        );

        var command = new ApplyAiAnalysisCommand
        {
            IncidentId = integrationEvent.IncidentId,
            SuggestedPriority = integrationEvent.SuggestedPriority,
            SuggestedCategory = integrationEvent.SuggestedCategory,
            Reasoning = integrationEvent.Reasoning,
            Confidence = integrationEvent.Confidence,
            RelatedChanges = (integrationEvent.RelatedChanges ?? [])
                .Select(change => new AiRelatedChange(change.Sha, change.Title, change.Author, change.CommittedAt, change.Url))
                .ToList(),
        };

        await _sender.Send(command, cancellationToken);

        _logger.LogInformation(
            "AI analysis applied to incident {IncidentId}.",
            integrationEvent.IncidentId
        );
    }
}
