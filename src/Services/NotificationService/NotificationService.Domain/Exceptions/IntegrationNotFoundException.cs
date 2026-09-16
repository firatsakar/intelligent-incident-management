using BuildingBlocks.SharedKernel.Exceptions;

namespace NotificationService.Domain.Exceptions;

public sealed class IntegrationNotFoundException : NotFoundException
{
    public IntegrationNotFoundException(Guid id)
        : base($"Integration with id '{id}' was not found.") { }
}
