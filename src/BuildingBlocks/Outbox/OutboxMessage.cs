namespace BuildingBlocks.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>
    /// Whose data the event is about, stamped when the row is written.
    /// </summary>
    /// <remarks>
    /// On the row rather than dug out of the payload. The row is written inside the transaction
    /// that changed the aggregate, in a scope that already knows the organisation; by the time the
    /// dispatcher picks it up, minutes later and in a scope of its own, that knowledge is gone.
    /// Reading it back out of the serialized event would mean every domain event carrying a field
    /// that is really a property of the delivery.
    /// </remarks>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The W3C trace context the row was written inside, so the publish it becomes continues that
    /// trace rather than starting a new one.
    /// </summary>
    /// <remarks>
    /// On the row for the same reason as <see cref="OrganizationId"/>: the dispatcher picks the row
    /// up later, on a timer, where nothing is in flight. Without it every outbox publish is the
    /// root of a trace of its own, and the chain an incident travels breaks at every service that
    /// writes one. Null when nothing was being traced at the time.
    /// </remarks>
    public string? TraceParent { get; init; }

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
