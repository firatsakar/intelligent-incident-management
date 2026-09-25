using BuildingBlocks.SharedKernel.Exceptions;

namespace IdentityService.Domain.Exceptions;

/// <summary>
/// One answer for an invitation or reset link that is unknown, expired, used or withdrawn. Telling
/// them apart would tell whoever holds a guessed or forwarded link which of those it is.
/// </summary>
public sealed class LinkNotFoundException()
    : NotFoundException("This link is not valid. It may have expired or already been used.");
