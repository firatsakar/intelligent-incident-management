using IdentityService.Application.DTOs;
using MediatR;

namespace IdentityService.Application.Queries.GetCurrentUser;

/// <summary>
/// Who the bearer of this token is, now. The claims in the token already say, but they were true
/// when it was minted — a role change or a deactivation since then is only visible here.
/// </summary>
public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<SessionUserDto?>;
