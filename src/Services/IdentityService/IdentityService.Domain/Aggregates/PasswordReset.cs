using BuildingBlocks.SharedKernel;

namespace IdentityService.Domain.Aggregates;

/// <summary>
/// A one-time link an Admin issues so a member can set a new password.
/// </summary>
/// <remarks>
/// Admin-issued only (Fırat, 2026-09-25): there is no "forgot password" yet, so nobody can ask for
/// a reset of an address they merely know. The organisation is carried so the Admin who issues it
/// and the member it is for are provably in the same one, and so the row can be listed by it.
/// </remarks>
public sealed class PasswordReset : AggregateRoot
{
    /// <summary>A day: long enough to reach someone, short enough that a stale link is not a spare key.</summary>
    public static readonly TimeSpan Validity = TimeSpan.FromHours(24);

    private PasswordReset() { }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = default!;

    public DateTime ExpiresAt { get; private set; }

    public Guid IssuedByUserId { get; private set; }

    public DateTime? UsedAt { get; private set; }

    /// <summary>Set when a newer reset for the same person replaces this one.</summary>
    public DateTime? RevokedAt { get; private set; }

    public static PasswordReset Issue(
        Guid organizationId,
        Guid userId,
        string tokenHash,
        Guid issuedByUserId,
        DateTime now
    )
    {
        return new PasswordReset
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = now + Validity,
            IssuedByUserId = issuedByUserId,
        };
    }

    public bool IsUsable(DateTime asOf) => UsedAt is null && RevokedAt is null && asOf < ExpiresAt;

    /// <summary>Spends the link. Once only, for the same reason an invitation is accepted once.</summary>
    public void Use(DateTime at)
    {
        if (!IsUsable(at))
            throw new InvalidOperationException("Only a usable reset link can be used.");

        UsedAt = at;
        SetUpdatedAt();
    }

    public void Revoke(DateTime at)
    {
        if (!IsUsable(at))
            return;

        RevokedAt = at;
        SetUpdatedAt();
    }
}
