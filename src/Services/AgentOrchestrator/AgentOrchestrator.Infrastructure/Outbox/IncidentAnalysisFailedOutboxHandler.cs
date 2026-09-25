using System.Text.Json;
using AgentOrchestrator.Domain.Events;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Outbox;

namespace AgentOrchestrator.Infrastructure.Outbox;

/// <summary>
/// The failure counterpart of <see cref="IncidentAnalysisCompletedOutboxHandler"/>, and a much
/// smaller job: there is no result to index, because nothing was produced. Publishing the event
/// is the whole of it.
/// </summary>
public sealed class IncidentAnalysisFailedOutboxHandler : IOutboxMessageHandler
{
    private readonly IEventBus _eventBus;

    public IncidentAnalysisFailedOutboxHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public string MessageType => nameof(IncidentAnalysisFailedDomainEvent);

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var domainEvent =
            JsonSerializer.Deserialize<IncidentAnalysisFailedDomainEvent>(message.Payload)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize payload for message {message.Id}."
            );

        await _eventBus.PublishAsync(
            new IncidentAnalysisFailedEvent
            {
                // The outbox row's Id, for the same reason as the completed handler: a fresh Guid
                // per attempt cannot serve as a consumer's idempotency key.
                Id = message.Id,

                // The organisation the row was stamped with when it was written, inside the
                // transaction that changed the aggregate. The dispatcher runs minutes later in a
                // scope of its own, which knows nothing about whose work this was.
                OrganizationId = message.OrganizationId,
                IncidentId = domainEvent.IncidentId,
                IncidentTitle = domainEvent.IncidentTitle,
                Error = domainEvent.ErrorMessage,
            },
            cancellationToken
        );
    }
}
