using System.Text.Json;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Events;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Outbox;

namespace AgentOrchestrator.Infrastructure.Outbox;

// The dual-route dispatch that used to be a switch inside the dispatcher: publish the integration
// event, then index the analysis for search. Search is a derived view, so indexing happens after
// the event rather than gating it.
public sealed class IncidentAnalysisCompletedOutboxHandler : IOutboxMessageHandler
{
    private readonly IEventBus _eventBus;
    private readonly IAnalysisIndexer _indexer;
    private readonly IIncidentAnalysisRepository _repository;

    public IncidentAnalysisCompletedOutboxHandler(
        IEventBus eventBus,
        IAnalysisIndexer indexer,
        IIncidentAnalysisRepository repository
    )
    {
        _eventBus = eventBus;
        _indexer = indexer;
        _repository = repository;
    }

    public string MessageType => nameof(IncidentAnalysisCompletedDomainEvent);

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var domainEvent =
            JsonSerializer.Deserialize<IncidentAnalysisCompletedDomainEvent>(message.Payload)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize payload for message {message.Id}."
            );

        var integrationEvent = new IncidentAnalyzedEvent
        {
            // Reuse the outbox row's Id so the event Id stays stable across retries — a fresh
            // Guid per attempt cannot serve as a consumer's idempotency key.
            Id = message.Id,

            // The organisation the row was stamped with when it was written, inside the
            // transaction that changed the aggregate. The dispatcher runs minutes later in a
            // scope of its own, which knows nothing about whose work this was.
            OrganizationId = message.OrganizationId,
            IncidentId = domainEvent.IncidentId,
            IncidentTitle = domainEvent.IncidentTitle,
            SuggestedCategory = domainEvent.SuggestedCategory,
            SuggestedPriority = domainEvent.SuggestedPriority,
            Reasoning = domainEvent.Reasoning,
            Confidence = domainEvent.Confidence,
        };

        await _eventBus.PublishAsync(integrationEvent, cancellationToken);

        var analysis = await _repository.GetByIncidentIdAsync(
            domainEvent.IncidentId,
            cancellationToken
        );

        await _indexer.IndexAsync(analysis, cancellationToken);
    }
}
