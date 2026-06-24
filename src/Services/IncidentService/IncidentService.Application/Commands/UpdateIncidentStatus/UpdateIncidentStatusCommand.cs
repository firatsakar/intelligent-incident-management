using IncidentService.Domain.Enums;
using MediatR;

namespace IncidentService.Application.Commands.UpdateIncidentStatus;

public sealed record UpdateIncidentStatusCommand : IRequest
{
    public required Guid IncidentId { get; init; }
    public required IncidentStatus NewStatus { get; init; }
}
