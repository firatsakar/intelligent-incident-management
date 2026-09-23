using BuildingBlocks.SharedKernel;
using IdentityService.Domain.Enums;

namespace IdentityService.Domain.Aggregates;

/// <summary>
/// A person who signs in, and the organisation whose data they see.
/// </summary>
/// <remarks>
/// One organisation per user, not a membership table. Signing in is then email plus password and
/// nothing else — the organisation is resolved from the account rather than asked for, which is
/// why the email is unique across the whole table and not just within an organisation. A person
/// who genuinely belongs to two organisations gets two accounts; when that stops being acceptable
/// the membership table can be introduced without changing what a token carries.
/// </remarks>
public sealed class User : AggregateRoot
{
    private User() { }

    public Guid OrganizationId { get; private set; }

    /// <summary>The normalised address. <see cref="Normalize"/> is what put it in this form.</summary>
    public string Email { get; private set; } = default!;

    public string DisplayName { get; private set; } = default!;

    /// <summary>A BCrypt hash. The plaintext never reaches this class.</summary>
    public string PasswordHash { get; private set; } = default!;

    public UserRole Role { get; private set; }

    /// <summary>
    /// A deactivated account keeps its history and its rows; what it loses is the ability to sign
    /// in. Deleting the user instead would orphan everything that names them.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// The form an address is stored and looked up in, so that two spellings of one address cannot
    /// become two accounts.
    /// </summary>
    /// <remarks>
    /// <c>ToLowerInvariant</c>, never <c>ToLower</c>. Under a Turkish culture — which is the
    /// culture this team's machines actually run — <c>ToLower</c> maps <c>I</c> to <c>ı</c>, so
    /// <c>FIRAT@…</c> and <c>firat@…</c> would normalise to two different strings and the same
    /// person would fail to sign in depending on how they typed their own name. Casing an address
    /// is a machine operation on ASCII, not a language one.
    /// </remarks>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static User Create(
        Guid organizationId,
        string email,
        string displayName,
        string passwordHash,
        UserRole role,
        bool isActive = true
    )
    {
        return new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = Normalize(email),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = isActive,
        };
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        SetUpdatedAt();
    }

    public void Rename(string displayName)
    {
        DisplayName = displayName.Trim();
        SetUpdatedAt();
    }

    public void ChangeRole(UserRole role)
    {
        Role = role;
        SetUpdatedAt();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAt();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdatedAt();
    }
}
