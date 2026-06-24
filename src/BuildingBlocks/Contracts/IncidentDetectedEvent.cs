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
    public Dictionary<string, string> Metadata { get; init; } = [];
}
