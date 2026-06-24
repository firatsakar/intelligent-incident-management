using IncidentService.Application.DTOs;
using IncidentService.Domain.Enums;
using MediatR;

namespace IncidentService.Application.Queries.GetIncidents;

public sealed record GetIncidentsQuery : IRequest<PagedResult<IncidentDto>>
{
    public IncidentStatus? Status { get; init; }
    public IncidentPriority? Priority { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
