using BuildingBlocks.SharedKernel;
using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using IdentityService.Application.Members;
using MediatR;

namespace IdentityService.Application.Commands.SetMemberActive;

public sealed class SetMemberActiveCommandHandler : IRequestHandler<SetMemberActiveCommand, MemberDto>
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IOrganizationContext _organization;

    public SetMemberActiveCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IOrganizationContext organization
    )
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _organization = organization;
    }

    public async Task<MemberDto> Handle(SetMemberActiveCommand request, CancellationToken cancellationToken)
    {
        var member = await MemberGuards.LoadAsync(_users, _organization, request.MemberId, cancellationToken);

        if (member.IsActive == request.IsActive)
            return MemberDto.FromDomain(member);

        if (request.IsActive)
        {
            member.Activate();
        }
        else
        {
            MemberGuards.NotSelf(request.ActorUserId, member, "deactivate");
            await MemberGuards.KeepsAnAdminAsync(_users, member, cancellationToken);

            member.Deactivate();

            // Every session the member still holds ends now: none of their refresh tokens will be
            // honoured again. The access token they hold lives out its remaining minutes — it
            // cannot be withdrawn, which is why it is short — and /me refuses them immediately,
            // which signs the console out on its next check.
            var now = DateTime.UtcNow;

            foreach (var token in await _refreshTokens.GetActiveByUserAsync(member.Id, now, cancellationToken))
                token.Revoke(now);
        }

        // One save: the users and refresh tokens live in one context, so the deactivation and the
        // revocations commit together or not at all.
        await _users.SaveChangesAsync(cancellationToken);

        return MemberDto.FromDomain(member);
    }
}
