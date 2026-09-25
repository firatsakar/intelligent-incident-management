using BuildingBlocks.SharedKernel;
using FluentValidation;
using FluentValidation.Results;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Accounts;
using IdentityService.Application.DTOs;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Commands.InviteMember;

public sealed class InviteMemberCommandHandler : IRequestHandler<InviteMemberCommand, InvitationIssuedDto>
{
    private readonly IUserRepository _users;
    private readonly IInvitationRepository _invitations;
    private readonly IOrganizationRepository _organizations;
    private readonly IEmailSender _email;
    private readonly IConsoleLinks _links;
    private readonly IOrganizationContext _organization;
    private readonly ILogger<InviteMemberCommandHandler> _logger;

    public InviteMemberCommandHandler(
        IUserRepository users,
        IInvitationRepository invitations,
        IOrganizationRepository organizations,
        IEmailSender email,
        IConsoleLinks links,
        IOrganizationContext organization,
        ILogger<InviteMemberCommandHandler> logger
    )
    {
        _users = users;
        _invitations = invitations;
        _organizations = organizations;
        _email = email;
        _links = links;
        _organization = organization;
        _logger = logger;
    }

    public async Task<InvitationIssuedDto> Handle(InviteMemberCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _organization.Required;
        var now = DateTime.UtcNow;

        // Addresses are unique across the whole platform (a person signs in with nothing else), so
        // an address with an account anywhere cannot be invited. Which organisation it is in is not
        // this Admin's to learn, so the message does not say — unless it is their own.
        var existing = await _users.GetByEmailAsync(request.Email, cancellationToken);

        if (existing is not null)
            throw new ValidationException(
                [
                    new ValidationFailure(
                        nameof(request.Email),
                        existing.OrganizationId == organizationId
                            ? "This person is already a member."
                            : "This address already has an account and cannot be invited."
                    ),
                ]
            );

        // A new invitation to the same address replaces any still pending, so only the newest
        // link works and a forwarded older one does not.
        foreach (var previous in await _invitations.ListPendingForEmailAsync(organizationId, request.Email, now, cancellationToken))
            previous.Revoke(now);

        var token = OneTimeToken.Generate();
        var invitation = Invitation.Issue(organizationId, request.Email, request.Role, token.Hash, request.ActorUserId, now);

        await _invitations.AddAsync(invitation, cancellationToken);
        await _invitations.SaveChangesAsync(cancellationToken);

        var link = _links.Invitation(token.Token);
        var organization = await _organizations.GetByIdAsync(organizationId, cancellationToken);
        var emailSent = await TrySendAsync(
            AccountEmails.Invitation(invitation.Email, organization?.Name ?? string.Empty, invitation.Role, link, invitation.ExpiresAt),
            cancellationToken
        );

        _logger.LogInformation(
            "Invitation {InvitationId} issued for organisation {OrganizationId} (email sent: {EmailSent}).",
            invitation.Id,
            organizationId,
            emailSent
        );

        return new InvitationIssuedDto(
            InvitationDto.FromDomain(invitation),
            new IssuedLinkDto(link, emailSent, invitation.ExpiresAt)
        );
    }

    // The invitation exists whether or not the mail server answered: the Admin is shown the link
    // either way and told which, and a mail outage does not become a failed invitation.
    private async Task<bool> TrySendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        try
        {
            await _email.SendAsync(email, cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The invitation email could not be sent; the link is still shown to the Admin.");

            return false;
        }
    }
}
