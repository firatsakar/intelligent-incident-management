using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using IdentityService.Domain.Aggregates;

namespace IdentityService.Application.Sessions;

/// <summary>
/// Turns a user into a session: one access token, one refresh token, and the row that makes the
/// second one revocable.
/// </summary>
/// <remarks>
/// Signing in and refreshing differ in how they decide <i>whether</i> to issue, and not at all in
/// what they issue. Keeping that second half here is what stops the two from drifting into
/// sessions with different claims or different lifetimes.
///
/// It does not save. Refreshing rotates the old row in the same unit of work as it writes the new
/// one, and splitting that across two saves would leave a window with two usable tokens.
/// </remarks>
public sealed class SessionIssuer
{
    private readonly ITokenGenerator _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IOrganizationRepository _organizations;

    public SessionIssuer(
        ITokenGenerator tokens,
        IRefreshTokenRepository refreshTokens,
        IOrganizationRepository organizations
    )
    {
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _organizations = organizations;
    }

    public async Task<IssuedSession> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetByIdAsync(user.OrganizationId, cancellationToken);

        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken();

        await _refreshTokens.AddAsync(
            RefreshToken.Issue(user.Id, refresh.Hash, refresh.ExpiresAt),
            cancellationToken
        );

        return new IssuedSession(Describe(user, organization), access, refresh);
    }

    /// <summary>
    /// The same shape <c>/me</c> returns, so restoring a session on a page load and signing in
    /// cannot disagree about who the reader is.
    /// </summary>
    public static SessionUserDto Describe(User user, Organization? organization) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.OrganizationId,
            // A user cannot exist without an organisation — the id is required and the row is
            // written in the same transaction — so a missing one is a broken database rather than
            // a state to render. Empty rather than a throw: the console shows the name in a corner,
            // and failing a sign-in over it would be the wrong trade.
            organization?.Name ?? string.Empty
        );
}
