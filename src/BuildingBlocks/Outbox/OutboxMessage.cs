namespace BuildingBlocks.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    // The domain event's type name. The dispatcher routes on this, matching it against the
    // registered handlers.
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
