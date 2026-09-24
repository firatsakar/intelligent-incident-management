using IdentityService.Application.Abstractions;

namespace IdentityService.Application.DTOs;

/// <summary>
/// Everything the console is told about whoever is signed in. It is also exactly what a screen is
/// allowed to decide anything from — the server decides again on every request regardless.
/// </summary>
public sealed record SessionUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid OrganizationId,
    string OrganizationName
);

/// <summary>
/// A newly issued session. The two tokens are here so the controller can put them in cookies;
/// neither of them reaches a response body, because a token in a body is a token in a log.
/// </summary>
public sealed record IssuedSession(
    SessionUserDto User,
    AccessToken Access,
    RefreshTokenPair Refresh
);
