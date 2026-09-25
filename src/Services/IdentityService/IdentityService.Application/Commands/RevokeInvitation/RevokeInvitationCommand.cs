using BuildingBlocks.SharedKernel;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Exceptions;
using MediatR;

namespace IdentityService.Application.Commands.RevokeInvitation;

public sealed record RevokeInvitationCommand(Guid InvitationId) : IRequest;

public sealed class RevokeInvitationCommandHandler : IRequestHandler<RevokeInvitationCommand>
{
    private readonly IInvitationRepository _invitations;
    private readonly IOrganizationContext _organization;

    public RevokeInvitationCommandHandler(IInvitationRepository invitations, IOrganizationContext organization)
    {
        _invitations = invitations;
        _organization = organization;
    }

    public async Task Handle(RevokeInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation =
            await _invitations.GetAsync(_organization.Required, request.InvitationId, cancellationToken)
            ?? throw new InvitationNotFoundException(request.InvitationId);

        invitation.Revoke(DateTime.UtcNow);
        await _invitations.SaveChangesAsync(cancellationToken);
    }
}
