using IncidentService.Application.Abstractions;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.UpdateIncidentStatus;

public sealed class UpdateIncidentStatusCommandHandler
    : IRequestHandler<UpdateIncidentStatusCommand>
{
    private readonly IIncidentRepository _repository;

    public UpdateIncidentStatusCommandHandler(IIncidentRepository repository)
    {
        _repository = repository;
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
    }
}
