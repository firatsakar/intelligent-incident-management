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
    private readonly IRealtimeNotifier _realtime;

    public CreateIncidentCommandHandler(
        IIncidentRepository repository,
        IEventBus eventBus,
        IRealtimeNotifier realtime)
    {
        _repository = repository;
        _eventBus = eventBus;
        _realtime = realtime;
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
            request.AssignedTeam,
            detectedAt: request.DetectedAt);

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

        // The DTO was already being built on the next line for the HTTP response, so carrying it
        // costs nothing. The client still re-reads the list — whether this incident belongs on
        // the first page of whatever filter someone has open is the server's call — but it can
        // fill the detail cache from this, so opening the row that just appeared is free.
        var dto = IncidentDto.FromDomain(incident);
        await _realtime.IncidentCreatedAsync(dto, cancellationToken);

        return dto;
    }
}