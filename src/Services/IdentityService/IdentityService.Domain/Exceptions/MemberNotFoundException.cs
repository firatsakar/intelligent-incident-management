using BuildingBlocks.SharedKernel.Exceptions;

namespace IdentityService.Domain.Exceptions;

// Also what another organisation's member answers: whether an account exists elsewhere is not this
// Admin's to learn.
public sealed class MemberNotFoundException(Guid id)
    : NotFoundException($"Member '{id}' was not found.");
