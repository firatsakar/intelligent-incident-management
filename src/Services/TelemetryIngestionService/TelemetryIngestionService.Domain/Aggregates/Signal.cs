using BuildingBlocks.SharedKernel;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Events;

namespace TelemetryIngestionService.Domain.Aggregates;

// A detection, not an incident. Signals are cheap and always recorded; promotion to an incident is
// a separate, deliberate decision.
public sealed class Signal : AggregateRoot
{
    private Dictionary<string, double> _scoreBreakdown = new();

    private Signal() { }

    /// <summary>
    /// Whose row this is. Inherited from the telemetry source the pipeline started at, carried
    /// here explicitly because nothing in this model has a navigation to inherit through.
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public Guid ErrorSignatureId { get; private set; }
    public SignalKind Kind { get; private set; }
    public SignalStatus Status { get; private set; }

    // When the underlying problem started, on the source clock — not when we detected it.
    public DateTime DetectedAt { get; private set; }

    public DateTime WindowStart { get; private set; }
    public DateTime WindowEnd { get; private set; }
    public long OccurrenceCount { get; private set; }

    public double Confidence { get; private set; }

    // Every component that contributed to the score, so a promotion can be explained after the
    // fact instead of being taken on faith.
    public IReadOnlyDictionary<string, double> ScoreBreakdown => _scoreBreakdown;

    public string? Reason { get; private set; }
    public Guid? IncidentId { get; private set; }

    public static Signal Detect(
        Guid organizationId,
        Guid errorSignatureId,
        SignalKind kind,
        DateTime detectedAt,
        DateTime windowStart,
        DateTime windowEnd,
        long occurrenceCount
    )
    {
        return new Signal
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ErrorSignatureId = errorSignatureId,
            Kind = kind,
            Status = SignalStatus.Recorded,
            DetectedAt = detectedAt,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            OccurrenceCount = occurrenceCount,
        };
    }

    public void Score(double confidence, IReadOnlyDictionary<string, double> breakdown)
    {
        Confidence = Math.Clamp(confidence, 0d, 1d);
        _scoreBreakdown = new Dictionary<string, double>(breakdown);
        SetUpdatedAt();
    }

    // The incident's id is chosen here rather than by IncidentService. At-least-once delivery
    // means the promotion event can arrive twice, and a caller-assigned key turns the second
    // arrival into a duplicate key rather than a second incident.
    public void Promote(
        Guid incidentId,
        string title,
        string description,
        string severity,
        string service,
        string fingerprint,
        string reason
    )
    {
        Status = SignalStatus.Promoted;
        IncidentId = incidentId;
        Reason = reason;
        SetUpdatedAt();

        AddDomainEvent(
            new SignalPromotedDomainEvent(
                Id,
                incidentId,
                title,
                description,
                severity,
                service,
                fingerprint,
                DetectedAt,
                OccurrenceCount,
                Confidence
            )
        );
    }

    public void MarkDeduplicated(Guid incidentId, string reason)
    {
        Status = SignalStatus.Deduplicated;
        IncidentId = incidentId;
        Reason = reason;
        SetUpdatedAt();
    }

    public void MarkWeak(string reason)
    {
        Status = SignalStatus.Weak;
        Reason = reason;
        SetUpdatedAt();
    }

    public void MarkSuppressed(string reason)
    {
        Status = SignalStatus.Suppressed;
        Reason = reason;
        SetUpdatedAt();
    }
}
