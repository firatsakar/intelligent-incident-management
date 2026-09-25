using System.Text.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Outbox;
using IncidentService.Domain.Events;

namespace IncidentService.Infrastructure.Outbox;

public sealed class IncidentResolvedOutboxHandler : IOutboxMessageHandler
{
    private readonly IEventBus _eventBus;

    public IncidentResolvedOutboxHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public string MessageType => nameof(IncidentResolvedDomainEvent);

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var domainEvent =
            JsonSerializer.Deserialize<IncidentResolvedDomainEvent>(message.Payload)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize payload for message {message.Id}."
            );

        await _eventBus.PublishAsync(
            new IncidentResolvedEvent
            {
                // The outbox row's id, so a retried dispatch publishes the same event id.
                Id = message.Id,

                // Stamped on the row inside the transaction that closed the incident; the
                // dispatcher's own scope knows nothing about whose incident it was.
                OrganizationId = message.OrganizationId,
                IncidentId = domainEvent.IncidentId,
                Status = domainEvent.Status.ToString(),
                Verdict = domainEvent.Verdict.ToString(),
                ResolvedAt = domainEvent.ResolvedAt,
            },
            cancellationToken
        );
    }
}
