using FluentValidation;
using FluentValidation.Results;
using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.UpdateIncidentStatus;

public sealed class UpdateIncidentStatusCommandHandler
    : IRequestHandler<UpdateIncidentStatusCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IRealtimeNotifier _realtime;
    private readonly IIncidentActivityRepository _activity;
    private readonly ICurrentUser _user;

    public UpdateIncidentStatusCommandHandler(
        IIncidentRepository repository,
        IRealtimeNotifier realtime,
        IIncidentActivityRepository activity,
        ICurrentUser user
    )
    {
        _repository = repository;
        _realtime = realtime;
        _activity = activity;
        _user = user;
    }

    public async Task Handle(
        UpdateIncidentStatusCommand request,
        CancellationToken cancellationToken
    )
    {
        var incident =
            await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new IncidentNotFoundException(request.IncidentId);

        // The rule belongs to the aggregate; asking it first turns a refusal into a 400 with the
        // field named, rather than an exception from inside the domain.
        if (incident.StatusChangeProblem(request.NewStatus, request.Verdict) is { } problem)
            throw new ValidationException([new ValidationFailure(nameof(request.Verdict), problem)]);

        var before = incident.Status;

        incident.UpdateStatus(request.NewStatus, request.Verdict);

        // Setting the status it already has records nothing: the trail is of changes.
        if (incident.Status != before)
            _activity.Add(IncidentActivity.StatusChanged(incident, before, _user.AsActor()));

        _repository.Update(incident);
        await _repository.SaveChangesAsync(cancellationToken);

        // After the save, never before: what is broadcast has to be what is stored.
        await _realtime.IncidentChangedAsync(
            IncidentDto.FromDomain(incident),
            cancellationToken
        );
    }
}
