namespace AgentOrchestrator.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    // Fully-qualified type name of the payload — dispatcher uses this to route
    // (e.g. "IncidentAnalyzedEvent" → RabbitMQ, an ES-doc type → Elasticsearch).
    public required string Type { get; init; }

    // Serialized event/payload (JSON).
    public required string Payload { get; init; }

    public DateTimeOffset OccurredOn { get; init; }

    // NULL until successfully dispatched. The dispatcher's WHERE filter.
    public DateTimeOffset? ProcessedOn { get; set; }

    // Populated on failure — for diagnostics and retry decisions.
    public string? Error { get; set; }

    public int RetryCount { get; set; }
}
