using IncidentService.Application.DTOs;
using MediatR;

namespace IncidentService.Application.Queries.GetIncidentById;

public sealed record GetIncidentByIdQuery(Guid Id) : IRequest<IncidentDto>;