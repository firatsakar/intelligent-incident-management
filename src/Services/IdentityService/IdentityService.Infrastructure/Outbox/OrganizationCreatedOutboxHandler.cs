using System.Text.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Outbox;
using IdentityService.Domain.Events;

namespace IdentityService.Infrastructure.Outbox;

public sealed class OrganizationCreatedOutboxHandler : IOutboxMessageHandler
{
    private readonly IEventBus _eventBus;

    public OrganizationCreatedOutboxHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public string MessageType => nameof(OrganizationCreatedDomainEvent);

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var domainEvent =
            JsonSerializer.Deserialize<OrganizationCreatedDomainEvent>(message.Payload)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize payload for message {message.Id}."
            );

        await _eventBus.PublishAsync(
            new OrganizationCreatedEvent
            {
                // The outbox row's id, so the event id is stable across retries and a consumer
                // that has already seen it can say so.
                Id = message.Id,
                // From the row, not the payload: the outbox row was stamped in the scope that
                // wrote it, and for this event that scope is the organisation being created.
                OrganizationId = message.OrganizationId,
                Name = domainEvent.Name,
            },
            cancellationToken
        );
    }
}
