using IncidentService.Domain.Enums;

namespace IncidentService.API.Contracts;

public sealed record CreateIncidentRequest
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IncidentPriority Priority { get; init; }
    public required IncidentSource Source { get; init; }
    public string? AssignedTeam { get; init; }
}
