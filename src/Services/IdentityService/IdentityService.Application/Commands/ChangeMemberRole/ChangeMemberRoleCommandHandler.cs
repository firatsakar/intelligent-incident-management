using BuildingBlocks.SharedKernel;
using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using IdentityService.Application.Members;
using IdentityService.Domain.Enums;
using MediatR;

namespace IdentityService.Application.Commands.ChangeMemberRole;

// The new role reaches the member's token on its next refresh — at most fifteen minutes — because
// a refresh reads the role from this row, not from the token it replaces.
public sealed class ChangeMemberRoleCommandHandler : IRequestHandler<ChangeMemberRoleCommand, MemberDto>
{
    private readonly IUserRepository _users;
    private readonly IOrganizationContext _organization;

    public ChangeMemberRoleCommandHandler(IUserRepository users, IOrganizationContext organization)
    {
        _users = users;
        _organization = organization;
    }

    public async Task<MemberDto> Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var member = await MemberGuards.LoadAsync(_users, _organization, request.MemberId, cancellationToken);

        if (member.Role == request.Role)
            return MemberDto.FromDomain(member);

        MemberGuards.NotSelf(request.ActorUserId, member, "change the role of");

        if (request.Role != UserRole.Admin)
            await MemberGuards.KeepsAnAdminAsync(_users, member, cancellationToken);

        member.ChangeRole(request.Role);
        await _users.SaveChangesAsync(cancellationToken);

        return MemberDto.FromDomain(member);
    }
}
