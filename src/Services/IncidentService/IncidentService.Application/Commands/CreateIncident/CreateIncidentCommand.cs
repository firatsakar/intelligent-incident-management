using IncidentService.Application.DTOs;
using IncidentService.Domain.Enums;
using MediatR;

namespace IncidentService.Application.Commands.CreateIncident;

public class CreateIncidentCommand : IRequest<IncidentDto>
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IncidentPriority Priority { get; init; }
    public required IncidentSource Source { get; init; }
    public string? AssignedTeam { get; init; }

    // Optional: when the problem actually started. Filing at 14:35 for something that began at
    // 14:20 and then correlating evidence around 14:35 finds nothing.
    public DateTime? DetectedAt { get; init; }
}
