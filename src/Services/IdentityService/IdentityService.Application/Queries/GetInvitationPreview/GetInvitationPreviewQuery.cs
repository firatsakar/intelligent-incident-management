using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Services;
using MediatR;

namespace IdentityService.Application.Queries.GetInvitationPreview;

public sealed record GetInvitationPreviewQuery(string Token) : IRequest<InvitationPreviewDto>;

public sealed class GetInvitationPreviewQueryHandler : IRequestHandler<GetInvitationPreviewQuery, InvitationPreviewDto>
{
    private readonly IInvitationRepository _invitations;
    private readonly IOrganizationRepository _organizations;

    public GetInvitationPreviewQueryHandler(IInvitationRepository invitations, IOrganizationRepository organizations)
    {
        _invitations = invitations;
        _organizations = organizations;
    }

    public async Task<InvitationPreviewDto> Handle(GetInvitationPreviewQuery request, CancellationToken cancellationToken)
    {
        var invitation = await _invitations.FindByTokenHashForAcceptAsync(OneTimeToken.Hash(request.Token), cancellationToken);

        if (invitation is null || !invitation.IsPending(DateTime.UtcNow))
            throw new LinkNotFoundException();

        var organization = await _organizations.GetByIdAsync(invitation.OrganizationId, cancellationToken);

        return new InvitationPreviewDto(organization?.Name ?? string.Empty, invitation.Email, invitation.Role, invitation.ExpiresAt);
    }
}
