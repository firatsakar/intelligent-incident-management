using BuildingBlocks.SharedKernel.Exceptions;

namespace IdentityService.Domain.Exceptions;

/// <summary>
/// One answer for a wrong setup code, an installation that already has users, and a setup that
/// already happened (Adım 25). Telling them apart would tell a caller what state the server is in.
/// </summary>
public sealed class SetupNotAvailableException()
    : NotFoundException("Setup is not available. Check the code in the identity service's log, or sign in.");
