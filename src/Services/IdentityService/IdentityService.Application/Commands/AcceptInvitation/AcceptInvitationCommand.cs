using FluentValidation;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Accounts;
using IdentityService.Application.DTOs;
using IdentityService.Application.Sessions;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Commands.AcceptInvitation;

public sealed record AcceptInvitationCommand(string Token, string DisplayName, string Password) : IRequest<IssuedSession>;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Password).NewPassword();
    }
}

/// <summary>
/// An invitation becomes an account, and the account is signed in.
/// </summary>
/// <remarks>
/// Which organisation the account joins, and as what, comes from the invitation's row and nothing
/// the request says: the person has no account yet and therefore no claim, so the row the link's
/// token finds is the only authority there is — the same way an ingest key's row is for a pushed
/// log batch.
/// </remarks>
public sealed class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, IssuedSession>
{
    private readonly IInvitationRepository _invitations;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly SessionIssuer _issuer;
    private readonly ILogger<AcceptInvitationCommandHandler> _logger;

    public AcceptInvitationCommandHandler(
        IInvitationRepository invitations,
        IUserRepository users,
        IPasswordHasher hasher,
        SessionIssuer issuer,
        ILogger<AcceptInvitationCommandHandler> logger
    )
    {
        _invitations = invitations;
        _users = users;
        _hasher = hasher;
        _issuer = issuer;
        _logger = logger;
    }

    public async Task<IssuedSession> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var invitation = await _invitations.FindByTokenHashForAcceptAsync(OneTimeToken.Hash(request.Token), cancellationToken);

        if (invitation is null || !invitation.IsPending(now))
            throw new LinkNotFoundException();

        // The address found an account between the invitation and now — invited twice to two
        // organisations, say. The link cannot be honoured, and saying why would say where.
        if (await _users.GetByEmailAsync(invitation.Email, cancellationToken) is not null)
            throw new LinkNotFoundException();

        var user = User.Create(
            invitation.OrganizationId,
            invitation.Email,
            request.DisplayName,
            _hasher.Hash(request.Password),
            invitation.Role
        );

        invitation.Accept(user.Id, now);

        await _users.AddAsync(user, cancellationToken);
        var session = await _issuer.IssueAsync(user, cancellationToken);

        // One save: the account, the spent invitation and the first session commit together.
        await _users.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Invitation {InvitationId} accepted: user {UserId} joined organisation {OrganizationId} as {Role}.",
            invitation.Id,
            user.Id,
            user.OrganizationId,
            user.Role
        );

        return session;
    }
}
