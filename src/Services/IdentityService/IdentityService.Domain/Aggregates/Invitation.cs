using BuildingBlocks.SharedKernel;
using IdentityService.Domain.Enums;

namespace IdentityService.Domain.Aggregates;

/// <summary>
/// An Admin's offer of an account in their organisation, to one address, with one role.
/// </summary>
/// <remarks>
/// <para>
/// How people join an organisation (Adım 16.5). There is no open sign-up: an open sign-up is anyone
/// opening an organisation, and every incident in one triggers a paid analysis. The Admin decides
/// who comes in and as what; the invitee decides only their own name and password.
/// </para>
/// <para>
/// The row, not the request, says which organisation the new account joins. Accepting is
/// anonymous — the person has no account yet — so the link's token finds this row and the row
/// carries the organisation, the same way an ingest key's row carries it for a pushed log batch.
/// </para>
/// </remarks>
public sealed class Invitation : AggregateRoot
{
    /// <summary>Long enough to survive a weekend and an inbox; short enough that a forwarded
    /// link does not stay a way in for ever.</summary>
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    private Invitation() { }

    public Guid OrganizationId { get; private set; }

    /// <summary>Normalised the way <see cref="User.Email"/> is, so the account it becomes can be found by it.</summary>
    public string Email { get; private set; } = default!;

    public UserRole Role { get; private set; }

    public string TokenHash { get; private set; } = default!;

    public DateTime ExpiresAt { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public DateTime? AcceptedAt { get; private set; }

    public Guid? AcceptedUserId { get; private set; }

    /// <summary>Set when an Admin withdraws it, or when a newer invitation to the same address replaces it.</summary>
    public DateTime? RevokedAt { get; private set; }

    public static Invitation Issue(
        Guid organizationId,
        string email,
        UserRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTime now
    )
    {
        return new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = User.Normalize(email),
            Role = role,
            TokenHash = tokenHash,
            ExpiresAt = now + Validity,
            InvitedByUserId = invitedByUserId,
        };
    }

    /// <summary>Still a way in: neither accepted, withdrawn, nor past its expiry.</summary>
    public bool IsPending(DateTime asOf) =>
        AcceptedAt is null && RevokedAt is null && asOf < ExpiresAt;

    /// <summary>
    /// Records that the invitation became an account. Once only: a second acceptance would be a
    /// second account from one link, which is exactly what a one-time link exists to prevent.
    /// </summary>
    public void Accept(Guid userId, DateTime at)
    {
        if (!IsPending(at))
            throw new InvalidOperationException("Only a pending invitation can be accepted.");

        AcceptedAt = at;
        AcceptedUserId = userId;
        SetUpdatedAt();
    }

    /// <summary>
    /// Withdraws it. Withdrawing one already accepted, withdrawn or expired changes nothing: the
    /// first ending is the one worth keeping.
    /// </summary>
    public void Revoke(DateTime at)
    {
        if (!IsPending(at))
            return;

        RevokedAt = at;
        SetUpdatedAt();
    }
}
