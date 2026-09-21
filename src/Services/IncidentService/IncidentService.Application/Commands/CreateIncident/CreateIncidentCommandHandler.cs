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

        // Only the id: whether this incident belongs on the first page of whatever filter someone
        // has open is the server's call, so the client asks rather than guesses.
        await _realtime.IncidentCreatedAsync(incident.Id, cancellationToken);

        return IncidentDto.FromDomain(incident);
    }
}