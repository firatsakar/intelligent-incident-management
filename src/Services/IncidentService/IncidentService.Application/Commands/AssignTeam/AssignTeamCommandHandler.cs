using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.AssignTeam;

public sealed class AssignTeamCommandHandler : IRequestHandler<AssignTeamCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IRealtimeNotifier _realtime;

    public AssignTeamCommandHandler(IIncidentRepository repository, IRealtimeNotifier realtime)
    {
        _repository = repository;
        _realtime = realtime;
    }

    public async Task Handle(AssignTeamCommand request, CancellationToken cancellationToken)
    {
        var incident =
            await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new IncidentNotFoundException(request.IncidentId);

        incident.AssignTeam(request.Team);

        _repository.Update(incident);
        await _repository.SaveChangesAsync(cancellationToken);

        await _realtime.IncidentChangedAsync(
            IncidentDto.FromDomain(incident),
            cancellationToken
        );
    }
}
