using BuildingBlocks.EventBus;

namespace BuildingBlocks.Contracts;

// Telemetry decided an error signature deserves an incident. The incident's id is chosen here
// rather than by IncidentService, for one specific reason: at-least-once delivery means this
// event can arrive twice, and a caller-assigned primary key makes the second arrival a duplicate
// key rather than a second incident.
public sealed record SignalPromotedEvent : IntegrationEvent
{
    public required Guid IncidentId { get; init; }
    public required Guid SignalId { get; init; }

    public required string Title { get; init; }

    // The evidence summary, already assembled. Compact by design: this is what the AI reads.
    public required string Description { get; init; }

    public required string Severity { get; init; }
    public required string Service { get; init; }
    public required string Fingerprint { get; init; }

    // When the problem started, on the source's clock — not when the incident was filed.
    public required DateTime DetectedAt { get; init; }

    public required long OccurrenceCount { get; init; }
    public required double Confidence { get; init; }
}
