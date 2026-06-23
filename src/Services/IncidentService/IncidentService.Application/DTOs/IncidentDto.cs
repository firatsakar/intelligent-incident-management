using IncidentService.Domain.Enums;

namespace IncidentService.Application.DTOs;

public sealed record IncidentDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = default!;
    public string Description { get; init; } = default!;
    public IncidentStatus Status { get; init; }
    public IncidentPriority Priority { get; init; }
    public IncidentSource Source { get; init; }
    public string? AssignedTeam { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}