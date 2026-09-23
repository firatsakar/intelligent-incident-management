using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using IdentityService.Application.Sessions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Commands.RefreshSession;

public sealed class RefreshSessionCommandHandler
    : IRequestHandler<RefreshSessionCommand, IssuedSession?>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository _users;
    private readonly ITokenGenerator _tokens;
    private readonly SessionIssuer _issuer;
    private readonly ILogger<RefreshSessionCommandHandler> _logger;

    public RefreshSessionCommandHandler(
        IRefreshTokenRepository refreshTokens,
        IUserRepository users,
        ITokenGenerator tokens,
        SessionIssuer issuer,
        ILogger<RefreshSessionCommandHandler> logger
    )
    {
        _refreshTokens = refreshTokens;
        _users = users;
        _tokens = tokens;
        _issuer = issuer;
        _logger = logger;
    }

    public async Task<IssuedSession?> Handle(
        RefreshSessionCommand request,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;

        var presented = await _refreshTokens.GetByHashAsync(
            _tokens.HashRefreshToken(request.RefreshToken),
            cancellationToken
        );

        // Never issued, or issued long enough ago that the cleanup sweep has been past. Nothing to
        // revoke and nothing to conclude.
        if (presented is null)
            return null;

        if (!presented.IsActive(now))
        {
            // Whether this is evidence of anything turns on ReplacedByHash, not on RevokedAt. A
            // token that was rotated has a successor, so the copy being presented should have been
            // discarded and was not. A token revoked by signing out has no successor — presenting
            // it again is a client that fired a refresh while the sign-out was still in flight,
            // which is an ordinary race and not a reason to end anybody's other sessions.
            await OnSpentTokenAsync(
                presented.UserId,
                wasRotated: presented.ReplacedByHash is not null,
                now,
                cancellationToken
            );

            return null;
        }

        var user = await _users.GetByIdAsync(presented.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            // The account went away or was switched off while the session was alive. The token is
            // still technically valid, which is exactly why it has to be ended here rather than
            // left to expire.
            presented.Revoke(now);

            await _refreshTokens.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Refresh rejected for {UserId}: the account is gone or deactivated.",
                presented.UserId
            );

            return null;
        }

        var session = await _issuer.IssueAsync(user, cancellationToken);

        // Rotation and issuance in one unit of work. Saving them separately would leave a moment
        // where both the old token and the new one are usable.
        presented.RotateTo(session.Refresh.Hash, now);

        await _refreshTokens.SaveChangesAsync(cancellationToken);

        return session;
    }

    /// <summary>
    /// A token that is no longer usable was presented. Expiring and being signed out are ordinary;
    /// being presented after it was already exchanged is not.
    /// </summary>
    private async Task OnSpentTokenAsync(
        Guid userId,
        bool wasRotated,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        if (!wasRotated)
        {
            // Too old, or signed out. Either way the client has to sign in again and there is
            // nothing here to defend against — ending this person's other sessions over it would
            // mean signing out of a laptop logs the phone out too.
            _logger.LogInformation(
                "Refresh rejected for {UserId}: the token had expired or been signed out.",
                userId
            );

            return;
        }

        // Someone presented a token that has already been exchanged. Either a copy was taken, or a
        // client kept a stale one — and from here those look identical. Ending every token the
        // user still holds is the only answer that is safe when it is the first case, and it costs
        // one sign-in when it is the second.
        var active = await _refreshTokens.GetActiveByUserAsync(userId, now, cancellationToken);

        foreach (var token in active)
            token.Revoke(now);

        await _refreshTokens.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "A spent refresh token was presented for {UserId}. Revoked {Count} still-active token(s) for that account.",
            userId,
            active.Count
        );
    }
}
