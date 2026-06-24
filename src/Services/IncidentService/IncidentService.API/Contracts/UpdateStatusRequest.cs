using IncidentService.Domain.Enums;

namespace IncidentService.API.Contracts;

public sealed record UpdateStatusRequest
{
    public required IncidentStatus NewStatus { get; init; }
}
