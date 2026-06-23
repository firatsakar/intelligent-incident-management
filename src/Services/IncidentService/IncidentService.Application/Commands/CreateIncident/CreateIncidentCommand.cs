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
}
