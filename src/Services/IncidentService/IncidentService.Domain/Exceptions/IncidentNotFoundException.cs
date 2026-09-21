using BuildingBlocks.SharedKernel.Exceptions;

namespace IncidentService.Domain.Exceptions;

public sealed class IncidentNotFoundException : NotFoundException
{
    public IncidentNotFoundException(Guid id)
        : base($"Incident with id '{id}' was not found.") { }
}
