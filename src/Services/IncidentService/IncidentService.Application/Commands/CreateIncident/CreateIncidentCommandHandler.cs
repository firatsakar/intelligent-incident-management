using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.SharedKernel;
using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.ValueObjects;
using MediatR;

namespace IncidentService.Application.Commands.CreateIncident;

public sealed class CreateIncidentCommandHandler
    : IRequestHandler<CreateIncidentCommand, IncidentDto>
{
    private readonly IIncidentRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IRealtimeNotifier _realtime;
    private readonly IOrganizationContext _organization;
    private readonly IIncidentActivityRepository _activity;
    private readonly ICurrentUser _user;

    public CreateIncidentCommandHandler(
        IIncidentRepository repository,
        IEventBus eventBus,
        IRealtimeNotifier realtime,
        IOrganizationContext organization,
        IIncidentActivityRepository activity,
        ICurrentUser user)
    {
        _repository = repository;
        _eventBus = eventBus;
        _realtime = realtime;
        _organization = organization;
        _activity = activity;
        _user = user;
    }

    public async Task<IncidentDto> Handle(
        CreateIncidentCommand request,
        CancellationToken cancellationToken)
    {
        var incident = Incident.Create(
            _organization.Required,
            request.Title,
            request.Description,
            request.Priority,
            request.Source,
            request.AssignedTeam,
            detectedAt: request.DetectedAt,
            externalId: request.ExternalId,
            reportedBy: request.ReportedBy);

        await _repository.AddAsync(incident, cancellationToken);

        // Opened by the key that sent it, or else by whoever is signed in.
        _activity.Add(IncidentActivity.Opened(
            incident,
            request.ReportedBy is { } key ? ActivityActor.ApiKey(key) : _user.AsActor()));

        await _repository.SaveChangesAsync(cancellationToken);

        // IncidentService has no outbox, so this publish is direct and the scope has to be read
        // here. Over HTTP it came from the claim; Required rather than the nullable accessor
        // because an incident announced to nobody in particular is worse than a failed request.
        await _eventBus.PublishAsync(new IncidentDetectedEvent
        {
            OrganizationId = _organization.Required,
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