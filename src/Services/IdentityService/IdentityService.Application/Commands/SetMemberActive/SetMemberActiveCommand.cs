using IdentityService.Application.DTOs;
using MediatR;

namespace IdentityService.Application.Commands.SetMemberActive;

public sealed record SetMemberActiveCommand(Guid ActorUserId, Guid MemberId, bool IsActive)
    : IRequest<MemberDto>;
