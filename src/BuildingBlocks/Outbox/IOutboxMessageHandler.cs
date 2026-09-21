namespace BuildingBlocks.Outbox;

// What a message *means* is the part that genuinely differs per service — AgentOrchestrator
// publishes an integration event and indexes into Elasticsearch, TelemetryIngestion publishes a
// promotion. That difference used to be a switch inside the dispatcher; it is now a registration.
public interface IOutboxMessageHandler
{
    // Matches OutboxMessage.Type, which is the domain event's type name.
    string MessageType { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
