using BuildingBlocks.SharedKernel;
using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using MediatR;

namespace IdentityService.Application.Queries.GetInvitations;

public sealed record GetInvitationsQuery : IRequest<IReadOnlyList<InvitationDto>>;

public sealed class GetInvitationsQueryHandler : IRequestHandler<GetInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    private readonly IInvitationRepository _invitations;
    private readonly IOrganizationContext _organization;

    public GetInvitationsQueryHandler(IInvitationRepository invitations, IOrganizationContext organization)
    {
        _invitations = invitations;
        _organization = organization;
    }

    public async Task<IReadOnlyList<InvitationDto>> Handle(GetInvitationsQuery request, CancellationToken cancellationToken)
    {
        var pending = await _invitations.ListPendingAsync(_organization.Required, DateTime.UtcNow, cancellationToken);

        return pending.Select(InvitationDto.FromDomain).ToList();
    }
}
