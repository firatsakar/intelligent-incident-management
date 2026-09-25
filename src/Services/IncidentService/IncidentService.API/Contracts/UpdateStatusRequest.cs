using IncidentService.Domain.Enums;

namespace IncidentService.API.Contracts;

public sealed record UpdateStatusRequest
{
    public required IncidentStatus NewStatus { get; init; }

    // Required when closing an open incident; see Incident.StatusChangeProblem.
    public IncidentVerdict? Verdict { get; init; }
}
