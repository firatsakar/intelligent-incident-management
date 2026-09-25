using FluentValidation;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Accounts;
using IdentityService.Application.DTOs;
using IdentityService.Application.Sessions;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Services;
using MediatR;

namespace IdentityService.Application.Commands.CompletePasswordReset;

public sealed record CompletePasswordResetCommand(string Token, string Password) : IRequest<IssuedSession>;

public sealed class CompletePasswordResetCommandValidator : AbstractValidator<CompletePasswordResetCommand>
{
    public CompletePasswordResetCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NewPassword();
    }
}

public sealed class CompletePasswordResetCommandHandler : IRequestHandler<CompletePasswordResetCommand, IssuedSession>
{
    private readonly IPasswordResetRepository _resets;
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly SessionIssuer _issuer;

    public CompletePasswordResetCommandHandler(
        IPasswordResetRepository resets,
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        SessionIssuer issuer
    )
    {
        _resets = resets;
        _users = users;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _issuer = issuer;
    }

    public async Task<IssuedSession> Handle(CompletePasswordResetCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var reset = await _resets.FindByTokenHashForResetAsync(OneTimeToken.Hash(request.Token), cancellationToken);

        if (reset is null || !reset.IsUsable(now))
            throw new LinkNotFoundException();

        var user = await _users.GetByIdAsync(reset.UserId, cancellationToken);

        if (user is null || !user.IsActive)
            throw new LinkNotFoundException();

        user.ChangePassword(_hasher.Hash(request.Password));
        reset.Use(now);

        // A reset is usually the answer to "someone may know my password". Every session that
        // password opened ends here; the one issued below is the only one left.
        foreach (var token in await _refreshTokens.GetActiveByUserAsync(user.Id, now, cancellationToken))
            token.Revoke(now);

        var session = await _issuer.IssueAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return session;
    }
}
