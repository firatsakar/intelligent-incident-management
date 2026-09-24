using BuildingBlocks.SharedKernel;

namespace TelemetryIngestionService.Domain.Aggregates;

// The running state of one distinct error, identified by its fingerprint. This is what makes
// deduplication, counters and the historical precedent weight possible.
public sealed class ErrorSignature : AggregateRoot
{
    private ErrorSignature() { }

    /// <summary>
    /// Whose row this is. Inherited from the telemetry source the pipeline started at, carried
    /// here explicitly because nothing in this model has a navigation to inherit through.
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public string Fingerprint { get; private set; } = default!;
    public string Service { get; private set; } = default!;
    public string? ExceptionType { get; private set; }
    public string NormalizedMessage { get; private set; } = default!;

    public DateTime FirstSeenAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public long OccurrenceCount { get; private set; }

    // Set while an incident opened for this signature is still open.
    public Guid? CurrentIncidentId { get; private set; }
    public DateTime? LastPromotedAt { get; private set; }

    // Occurrences attributed to the currently open incident — the counter that is bumped instead
    // of opening a duplicate.
    public long CurrentIncidentOccurrences { get; private set; }

    public bool IsMuted { get; private set; }

    // Precedent, the input that lets scoring improve over time.
    public int PromotionCount { get; private set; }
    public int ConfirmedRealCount { get; private set; }
    public int FalsePositiveCount { get; private set; }

    public static ErrorSignature Create(
        Guid organizationId,
        string fingerprint,
        string service,
        string? exceptionType,
        string normalizedMessage,
        DateTime seenAt
    )
    {
        return new ErrorSignature
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Fingerprint = fingerprint,
            Service = service,
            ExceptionType = exceptionType,
            NormalizedMessage = normalizedMessage,
            FirstSeenAt = seenAt,
            LastSeenAt = seenAt,
            OccurrenceCount = 1,
        };
    }

    public void RecordOccurrence(DateTime seenAt, long count = 1)
    {
        OccurrenceCount += count;

        // Source clocks can deliver out of order; first and last seen must stay true bounds.
        if (seenAt < FirstSeenAt)
            FirstSeenAt = seenAt;

        if (seenAt > LastSeenAt)
            LastSeenAt = seenAt;

        if (CurrentIncidentId.HasValue)
            CurrentIncidentOccurrences += count;

        SetUpdatedAt();
    }

    public void AttachIncident(Guid incidentId, DateTime promotedAt)
    {
        CurrentIncidentId = incidentId;
        LastPromotedAt = promotedAt;
        CurrentIncidentOccurrences = 0;
        PromotionCount++;
        SetUpdatedAt();
    }

    // Called when the incident this signature opened is resolved. Which counter moves decides how
    // the signature is scored next time it fires.
    public void DetachIncident(bool wasRealIncident)
    {
        CurrentIncidentId = null;
        CurrentIncidentOccurrences = 0;

        if (wasRealIncident)
            ConfirmedRealCount++;
        else
            FalsePositiveCount++;

        SetUpdatedAt();
    }

    public void Mute()
    {
        IsMuted = true;
        SetUpdatedAt();
    }

    public void Unmute()
    {
        IsMuted = false;
        SetUpdatedAt();
    }

    // The ageing rule: an open incident only absorbs a new burst while the signature is still
    // active. Once it has been quiet longer than the dedup window, the next burst deserves a new
    // incident rather than bumping a stale counter.
    public bool CanAbsorbInto(DateTime now, TimeSpan dedupWindow)
    {
        return CurrentIncidentId.HasValue && now - LastSeenAt <= dedupWindow;
    }
}
