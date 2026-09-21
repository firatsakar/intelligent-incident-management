using BuildingBlocks.SharedKernel;

namespace TelemetryIngestionService.Domain.Events;

public sealed record SignalPromotedDomainEvent(
    Guid SignalId,
    Guid IncidentId,
    string Title,
    string Description,
    string Severity,
    string Service,
    string Fingerprint,
    DateTime DetectedAt,
    long OccurrenceCount,
    double Confidence
) : DomainEvent;
