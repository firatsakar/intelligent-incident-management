using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using IdentityService.Application.Sessions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Commands.SignIn;

public sealed class SignInCommandHandler : IRequestHandler<SignInCommand, IssuedSession?>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly SessionIssuer _issuer;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ILogger<SignInCommandHandler> _logger;

    public SignInCommandHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        SessionIssuer issuer,
        IRefreshTokenRepository refreshTokens,
        ILogger<SignInCommandHandler> logger
    )
    {
        _users = users;
        _hasher = hasher;
        _issuer = issuer;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

    public async Task<IssuedSession?> Handle(
        SignInCommand request,
        CancellationToken cancellationToken
    )
    {
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            // Verify against a hash of nothing rather than returning here. Sign-in is slow on
            // purpose, so an early return turns that slowness into an answer: a few milliseconds
            // means no account, a few hundred means there is one. The wasted work costs a request
            // that was going to fail anyway.
            _hasher.Verify(request.Password, _hasher.DummyHash);

            _logger.LogInformation("Sign-in rejected: no account for the address given.");

            return null;
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogInformation("Sign-in rejected for {UserId}: password did not match.", user.Id);

            return null;
        }

        if (!user.IsActive)
        {
            // Same answer as a wrong password, and for the same reason. The distinction is in the
            // log, where it is useful, rather than in the response, where it is a disclosure.
            _logger.LogInformation("Sign-in rejected for {UserId}: account is deactivated.", user.Id);

            return null;
        }

        var session = await _issuer.IssueAsync(user, cancellationToken);

        await _refreshTokens.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Signed in {UserId} of organisation {OrganizationId}.", user.Id, user.OrganizationId);

        return session;
    }
}
