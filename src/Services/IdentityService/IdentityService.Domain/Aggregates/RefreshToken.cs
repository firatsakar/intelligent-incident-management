using BuildingBlocks.SharedKernel;

namespace IdentityService.Domain.Aggregates;

/// <summary>
/// One issued refresh token, and everything that has happened to it.
/// </summary>
/// <remarks>
/// <para>
/// The access token is short-lived and carries its own claims, so nothing about it is stored. This
/// row is the other half: the thing a browser can present, days later, to be given a new one — and
/// therefore the only part of a session that can be taken away.
/// </para>
/// <para>
/// <b>What is stored is a SHA-256 of the token, not a BCrypt hash.</b> Two reasons, and the second
/// is the one that settles it. A refresh token is 256 bits of randomness rather than something a
/// person chose, so a deliberately slow hash defends against nothing — there is no guessing to
/// slow down. And the row has to be <i>found</i> by what the browser presented, which a per-row
/// salt makes impossible without reading every row and testing each one. Passwords keep BCrypt,
/// where the slowness is the whole point.
/// </para>
/// <para>
/// A used token is not deleted; it is revoked and told what replaced it. That chain is what makes
/// reuse visible: presenting a token that has already been rotated means either a stolen copy or a
/// client with a stale one, and the caller cannot tell which, so it revokes the whole chain.
/// </para>
/// </remarks>
public sealed class RefreshToken : AggregateRoot
{
    private RefreshToken() { }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = default!;

    public DateTime ExpiresAt { get; private set; }

    /// <summary>Null while the token is still usable. Set by rotation, logout, or reuse detection.</summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>The hash of the token issued in this one's place, when it was rotated rather than revoked outright.</summary>
    public string? ReplacedByHash { get; private set; }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime expiresAt)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Usable: neither revoked nor past its expiry.</summary>
    public bool IsActive(DateTime asOf) => RevokedAt is null && asOf < ExpiresAt;

    /// <summary>
    /// Ends the token. Revoking an already-revoked one does nothing: the first revocation time is
    /// evidence of when the session actually ended, and a later logout or sweep should not
    /// overwrite it with its own.
    /// </summary>
    public void Revoke(DateTime at)
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = at;
        SetUpdatedAt();
    }

    /// <summary>
    /// Retires this token in favour of its successor. The caller checks <see cref="IsActive"/>
    /// first — rotating a token that is already spent is the reuse case, which is a decision about
    /// the whole chain and not something this row can make on its own.
    /// </summary>
    public void RotateTo(string replacementHash, DateTime at)
    {
        if (!IsActive(at))
            throw new InvalidOperationException(
                "Only an active refresh token can be rotated. A spent one is a reuse to be investigated, not a rotation."
            );

        ReplacedByHash = replacementHash;
        RevokedAt = at;
        SetUpdatedAt();
    }
}
