using System.Linq.Expressions;

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

    /// <summary>Not before this, after a failure (Adım 28). Null: as soon as the dispatcher looks.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>
    /// Set when the row gave up after <see cref="MaxAttempts"/> failures (Adım 28). A parked row is
    /// never dispatched again on its own; clearing this puts it back.
    /// </summary>
    public DateTimeOffset? ParkedAt { get; set; }

    // ---- retry policy (Adım 28) ---------------------------------------------------------------
    //
    // Every five seconds, forever, was the policy until now, and a failure that is not going to
    // go away — a bug in a handler — turned into the same event republished every five seconds for
    // as long as nobody looked: 519 attempts once, in Adım 17. A cap alone would be the opposite
    // mistake: at five seconds a try, a broker restarting for a minute would park everything
    // written during it. So the wait grows, and only a failure that outlasts hours of retrying
    // parks the row.

    /// <summary>The attempt that parks the row: about 3.2 hours after the first failure.</summary>
    public const int MaxAttempts = 20;

    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(15);

    /// <summary>The wait after the given number of failures: 5 s, 10 s, 20 s … never over 15 min.</summary>
    public static TimeSpan RetryDelay(int failures) =>
        TimeSpan.FromSeconds(
            Math.Min(FirstRetryDelay.TotalSeconds * Math.Pow(2, Math.Max(0, failures - 1)), MaxRetryDelay.TotalSeconds)
        );

    /// <summary>
    /// Rows the dispatcher should take now: not yet dispatched, not parked, and not waiting out a
    /// retry delay. One definition for every service's store.
    /// </summary>
    public static Expression<Func<OutboxMessage, bool>> Due(DateTimeOffset now) =>
        message =>
            message.ProcessedOn == null
            && message.ParkedAt == null
            && (message.NextAttemptAt == null || message.NextAttemptAt <= now);

    /// <summary>The side effect happened; the row is done.</summary>
    public void MarkDispatched(DateTimeOffset now)
    {
        ProcessedOn = now;
        Error = null;
        NextAttemptAt = null;
    }

    /// <summary>Records a failed attempt. True when this one parked the row.</summary>
    public bool MarkFailed(string error, DateTimeOffset now)
    {
        RetryCount++;
        Error = error;

        if (RetryCount >= MaxAttempts)
        {
            ParkedAt = now;
            NextAttemptAt = null;

            return true;
        }

        NextAttemptAt = now + RetryDelay(RetryCount);

        return false;
    }
}
