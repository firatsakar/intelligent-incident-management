using BuildingBlocks.SharedKernel;
using FluentValidation;
using FluentValidation.Results;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Accounts;
using IdentityService.Application.DTOs;
using IdentityService.Application.Members;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Commands.IssuePasswordReset;

public sealed record IssuePasswordResetCommand(Guid ActorUserId, Guid MemberId) : IRequest<IssuedLinkDto>;

public sealed class IssuePasswordResetCommandHandler : IRequestHandler<IssuePasswordResetCommand, IssuedLinkDto>
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetRepository _resets;
    private readonly IOrganizationRepository _organizations;
    private readonly IEmailSender _email;
    private readonly IConsoleLinks _links;
    private readonly IOrganizationContext _organization;
    private readonly ILogger<IssuePasswordResetCommandHandler> _logger;

    public IssuePasswordResetCommandHandler(
        IUserRepository users,
        IPasswordResetRepository resets,
        IOrganizationRepository organizations,
        IEmailSender email,
        IConsoleLinks links,
        IOrganizationContext organization,
        ILogger<IssuePasswordResetCommandHandler> logger
    )
    {
        _users = users;
        _resets = resets;
        _organizations = organizations;
        _email = email;
        _links = links;
        _organization = organization;
        _logger = logger;
    }

    public async Task<IssuedLinkDto> Handle(IssuePasswordResetCommand request, CancellationToken cancellationToken)
    {
        var member = await MemberGuards.LoadAsync(_users, _organization, request.MemberId, cancellationToken);

        // One's own password is changed in Profile, with the current one — not by a link that
        // would also end one's own sessions.
        MemberGuards.NotSelf(request.ActorUserId, member, "issue a reset link for");

        if (!member.IsActive)
            throw new ValidationException(
                [new ValidationFailure("Member", "This account is deactivated. Activate it first.")]
            );

        var now = DateTime.UtcNow;

        // Only the newest link works.
        foreach (var previous in await _resets.ListUsableForUserAsync(member.Id, now, cancellationToken))
            previous.Revoke(now);

        var token = OneTimeToken.Generate();
        var reset = PasswordReset.Issue(member.OrganizationId, member.Id, token.Hash, request.ActorUserId, now);

        await _resets.AddAsync(reset, cancellationToken);
        await _resets.SaveChangesAsync(cancellationToken);

        var link = _links.PasswordReset(token.Token);
        var organization = await _organizations.GetByIdAsync(member.OrganizationId, cancellationToken);
        var sent = true;

        try
        {
            await _email.SendAsync(
                AccountEmails.PasswordReset(member.Email, organization?.Name ?? string.Empty, link, reset.ExpiresAt),
                cancellationToken
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The reset email could not be sent; the link is still shown to the Admin.");
            sent = false;
        }

        return new IssuedLinkDto(link, sent, reset.ExpiresAt);
    }
}
