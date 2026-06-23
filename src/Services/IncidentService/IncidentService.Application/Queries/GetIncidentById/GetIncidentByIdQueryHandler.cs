using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Queries.GetIncidentById;

public sealed class GetIncidentByIdQueryHandler
    : IRequestHandler<GetIncidentByIdQuery, IncidentDto>
{
    private readonly IIncidentRepository _repository;

    public GetIncidentByIdQueryHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IncidentDto> Handle(
        GetIncidentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new IncidentNotFoundException(request.Id);

        return new IncidentDto
        {
            Id = incident.Id,
            Title = incident.Title,
            Description = incident.Description,
            Status = incident.Status,
            Priority = incident.Priority,
            Source = incident.Source,
            AssignedTeam = incident.AssignedTeam,
            CreatedAt = incident.CreatedAt,
            UpdatedAt = incident.UpdatedAt
        };
    }
}