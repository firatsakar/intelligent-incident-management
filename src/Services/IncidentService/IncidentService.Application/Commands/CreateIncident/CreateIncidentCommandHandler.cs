using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using MediatR;

namespace IncidentService.Application.Commands.CreateIncident;

public sealed class CreateIncidentCommandHandler
    : IRequestHandler<CreateIncidentCommand, IncidentDto>
{
    private readonly IIncidentRepository _repository;
    private readonly IEventBus _eventBus;

    public CreateIncidentCommandHandler(
        IIncidentRepository repository,
        IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task<IncidentDto> Handle(
        CreateIncidentCommand request,
        CancellationToken cancellationToken)
    {
        var incident = Incident.Create(
            request.Title,
            request.Description,
            request.Priority,
            request.Source,
            request.AssignedTeam);

        await _repository.AddAsync(incident, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new IncidentDetectedEvent
        {
            IncidentId = incident.Id,
            Title = incident.Title,
            Description = incident.Description,
            Severity = incident.Priority.ToString(),
            Source = incident.Source.ToString()
        }, cancellationToken);

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