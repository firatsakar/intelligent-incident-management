using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.AssignTeam;

public sealed class AssignTeamCommandHandler : IRequestHandler<AssignTeamCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IRealtimeNotifier _realtime;
    private readonly IIncidentActivityRepository _activity;
    private readonly ICurrentUser _user;

    public AssignTeamCommandHandler(
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

    public async Task Handle(AssignTeamCommand request, CancellationToken cancellationToken)
    {
        var incident =
            await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new IncidentNotFoundException(request.IncidentId);

        var before = incident.AssignedTeam;

        incident.AssignTeam(request.Team);

        var recorded = incident.AssignedTeam == before
            ? null
            : IncidentActivity.TeamAssigned(incident, before, _user.AsActor());

        if (recorded is not null)
            _activity.Add(recorded);

        _repository.Update(incident);
        await _repository.SaveChangesAsync(cancellationToken);

        await _realtime.IncidentChangedAsync(
            IncidentDto.FromDomain(incident),
            cancellationToken
        );
        await _realtime.ActivityRecordedAsync(recorded, cancellationToken);
    }
}
