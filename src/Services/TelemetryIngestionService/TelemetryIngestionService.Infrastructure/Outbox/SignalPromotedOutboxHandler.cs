using System.Text.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Outbox;
using TelemetryIngestionService.Domain.Events;

namespace TelemetryIngestionService.Infrastructure.Outbox;

public sealed class SignalPromotedOutboxHandler : IOutboxMessageHandler
{
    private readonly IEventBus _eventBus;

    public SignalPromotedOutboxHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public string MessageType => nameof(SignalPromotedDomainEvent);

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var domainEvent =
            JsonSerializer.Deserialize<SignalPromotedDomainEvent>(message.Payload)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize payload for message {message.Id}."
            );

        await _eventBus.PublishAsync(
            new SignalPromotedEvent
            {
                // The outbox row's id, so the event id is stable across retries.
                Id = message.Id,
                IncidentId = domainEvent.IncidentId,
                SignalId = domainEvent.SignalId,
                Title = domainEvent.Title,
                Description = domainEvent.Description,
                Severity = domainEvent.Severity,
                Service = domainEvent.Service,
                Fingerprint = domainEvent.Fingerprint,
                DetectedAt = domainEvent.DetectedAt,
                OccurrenceCount = domainEvent.OccurrenceCount,
                Confidence = domainEvent.Confidence,
            },
            cancellationToken
        );
    }
}
