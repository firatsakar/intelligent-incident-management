using BuildingBlocks.EventBus;

namespace BuildingBlocks.Contracts;

public sealed record IncidentDetectedEvent : IntegrationEvent
{
    public required Guid IncidentId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Severity { get; init; }   // Critical, High, Medium, Low
    public required string Source { get; init; }     // Manual, Telemetry, Alert
    public string? AssignedTeam { get; init; }

    // When the problem started, when that is known. Null for an incident filed without one.
    public DateTime? DetectedAt { get; init; }

    // The service the telemetry says is failing, when a signal opened the incident. The analysis
    // uses it to find the repository that service's code lives in (Adım 17.5). Null for an
    // incident filed by hand, which names no service.
    public string? Service { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}
