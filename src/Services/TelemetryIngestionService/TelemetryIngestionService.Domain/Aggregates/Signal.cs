using BuildingBlocks.SharedKernel;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Domain.Aggregates;

// A detection, not an incident. Signals are cheap and always recorded; promotion to an incident is
// a separate, deliberate decision.
public sealed class Signal : AggregateRoot
{
    private Dictionary<string, double> _scoreBreakdown = new();

    private Signal() { }

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

    public void MarkPromoted(Guid incidentId, string reason)
    {
        Status = SignalStatus.Promoted;
        IncidentId = incidentId;
        Reason = reason;
        SetUpdatedAt();
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
