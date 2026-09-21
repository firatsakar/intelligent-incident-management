using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.UpdateIncidentStatus;

public sealed class UpdateIncidentStatusCommandHandler
    : IRequestHandler<UpdateIncidentStatusCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IRealtimeNotifier _realtime;

    public UpdateIncidentStatusCommandHandler(
        IIncidentRepository repository,
        IRealtimeNotifier realtime
    )
    {
        _repository = repository;
        _realtime = realtime;
    }

    public async Task Handle(
        UpdateIncidentStatusCommand request,
        CancellationToken cancellationToken
    )
    {
        var incident =
            await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new IncidentNotFoundException(request.IncidentId);

        incident.UpdateStatus(request.NewStatus);

        _repository.Update(incident);
        await _repository.SaveChangesAsync(cancellationToken);

        // After the save, never before: what is broadcast has to be what is stored.
        await _realtime.IncidentChangedAsync(
            IncidentDto.FromDomain(incident),
            cancellationToken
        );
    }
}
