using IdentityService.Application.DTOs;
using IdentityService.Domain.Enums;
using MediatR;

namespace IdentityService.Application.Commands.InviteMember;

public sealed record InviteMemberCommand(Guid ActorUserId, string Email, UserRole Role)
    : IRequest<InvitationIssuedDto>;
