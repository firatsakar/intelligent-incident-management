using IdentityService.Application.DTOs;
using IdentityService.Domain.Enums;
using MediatR;

namespace IdentityService.Application.Commands.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(Guid ActorUserId, Guid MemberId, UserRole Role)
    : IRequest<MemberDto>;
