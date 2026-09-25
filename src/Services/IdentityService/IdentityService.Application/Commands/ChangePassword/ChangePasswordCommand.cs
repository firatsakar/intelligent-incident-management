using FluentValidation;
using FluentValidation.Results;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Accounts;
using IdentityService.Application.DTOs;
using IdentityService.Application.Sessions;
using MediatR;

namespace IdentityService.Application.Commands.ChangePassword;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<IssuedSession>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NewPassword();
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("Choose a password different from the current one.");
    }
}

/// <summary>
/// One's own password, with the current one as proof. Every other session ends and this device
/// gets a fresh one — the usual reason to change a password is not trusting where the old one went.
/// </summary>
public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, IssuedSession>
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly SessionIssuer _issuer;

    public ChangePasswordCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        SessionIssuer issuer
    )
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _issuer = issuer;
    }

    public async Task<IssuedSession> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null || !user.IsActive || !_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException(
                [new ValidationFailure(nameof(request.CurrentPassword), "The current password is not correct.")]
            );

        var now = DateTime.UtcNow;

        user.ChangePassword(_hasher.Hash(request.NewPassword));

        foreach (var token in await _refreshTokens.GetActiveByUserAsync(user.Id, now, cancellationToken))
            token.Revoke(now);

        var session = await _issuer.IssueAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return session;
    }
}
