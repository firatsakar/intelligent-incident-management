using MediatR;

namespace IdentityService.Application.Commands.SignOut;

/// <summary>
/// Ends one session. Not every session the person has — signing out of a laptop should not sign
/// them out of a phone, and the sweeping version belongs to reuse detection, where it means
/// something.
/// </summary>
public sealed record SignOutCommand(string? RefreshToken) : IRequest;
