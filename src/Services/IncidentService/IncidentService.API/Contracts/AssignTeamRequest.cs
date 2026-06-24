namespace IncidentService.API.Contracts;

public sealed record AssignTeamRequest
{
    public required string Team { get; init; }
}
