using BuildingBlocks.SharedKernel.Exceptions;

namespace IdentityService.Domain.Exceptions;

public sealed class InvitationNotFoundException(Guid id)
    : NotFoundException($"Invitation '{id}' was not found.");
