using BuildingBlocks.SharedKernel;

namespace TelemetryIngestionService.Domain.Aggregates;

// How far a source has been read. Kept in its own table rather than on TelemetrySource because it
// is written on every poll, while the source row is configuration that rarely changes.
public sealed class SourceCursor : AggregateRoot
{
    private SourceCursor() { }

    /// <summary>
    /// Whose row this is. Inherited from the telemetry source the pipeline started at, carried
    /// here explicitly because nothing in this model has a navigation to inherit through.
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public Guid TelemetrySourceId { get; private set; }

    // Opaque to us — each connector decides what it means (a Seq event id, an ES search_after
    // token, an offset). Only the connector that wrote it ever interprets it.
    public string? Position { get; private set; }

    // The source-clock timestamp of the newest event consumed so far. Used as a fallback when a
    // connector has no positional token, and for diagnosing ingestion lag.
    public DateTime? LastEventTimestamp { get; private set; }

    public DateTime? LastPolledAt { get; private set; }
    public string? LastError { get; private set; }

    public static SourceCursor Start(Guid organizationId, Guid telemetrySourceId)
    {
        return new SourceCursor
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            TelemetrySourceId = telemetrySourceId,
        };
    }

    public void Advance(string? position, DateTime? lastEventTimestamp)
    {
        Position = position;

        // Never move backwards: a connector returning an older batch must not rewind the cursor.
        if (lastEventTimestamp.HasValue
            && (!LastEventTimestamp.HasValue || lastEventTimestamp > LastEventTimestamp))
        {
            LastEventTimestamp = lastEventTimestamp;
        }

        LastPolledAt = DateTime.UtcNow;
        LastError = null;
        SetUpdatedAt();
    }

    public void RecordFailure(string error)
    {
        LastPolledAt = DateTime.UtcNow;
        LastError = error;
        SetUpdatedAt();
    }
}
