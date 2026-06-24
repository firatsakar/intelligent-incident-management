using MediatR;

namespace IncidentService.Application.Commands.AssignTeam;

public sealed record AssignTeamCommand : IRequest
{
    public required Guid IncidentId { get; init; }
    public required string Team { get; init; }
}
