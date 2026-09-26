using BuildingBlocks.SharedKernel.Exceptions;

namespace IncidentService.Domain.Exceptions;

public sealed class IncidentApiKeyNotFoundException : NotFoundException
{
    public IncidentApiKeyNotFoundException(Guid id)
        : base($"API key with id '{id}' was not found.") { }
}
