using BuildingBlocks.SharedKernel;
using FluentValidation;
using FluentValidation.Results;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using IdentityService.Domain.Exceptions;

namespace IdentityService.Application.Members;

/// <summary>
/// The rules every change to a member passes through, in one place so the role change and the
/// deactivation cannot drift into different answers to the same question.
/// </summary>
public static class MemberGuards
{
    /// <summary>
    /// The member, if they belong to the caller's organisation. The identity tables carry no query
    /// filter — sign-in precedes any organisation — so the comparison is made here, explicitly, and
    /// another organisation's member is not found rather than forbidden.
    /// </summary>
    public static async Task<User> LoadAsync(
        IUserRepository users,
        IOrganizationContext organization,
        Guid memberId,
        CancellationToken cancellationToken
    )
    {
        var user = await users.GetByIdAsync(memberId, cancellationToken);

        return user is not null && user.OrganizationId == organization.Required
            ? user
            : throw new MemberNotFoundException(memberId);
    }

    /// <summary>
    /// Nobody changes their own role or switches off their own account: an Admin demoting or
    /// locking themselves out is a mistake with nobody left to undo it, and there is always another
    /// Admin who can do it deliberately.
    /// </summary>
    public static void NotSelf(Guid actorUserId, User member, string action)
    {
        if (member.Id == actorUserId)
            Refuse($"You cannot {action} yourself. Ask another Admin.");
    }

    /// <summary>
    /// An organisation always keeps an Admin who can sign in. Without one, nobody could invite,
    /// promote, or reset anyone again, and the organisation would be locked for good.
    /// </summary>
    public static async Task KeepsAnAdminAsync(
        IUserRepository users,
        User member,
        CancellationToken cancellationToken
    )
    {
        if (member.Role != UserRole.Admin || !member.IsActive)
            return;

        if (await users.CountActiveAdminsAsync(member.OrganizationId, cancellationToken) <= 1)
            Refuse("This is the organisation's last active Admin. Make someone else an Admin first.");
    }

    private static void Refuse(string message) =>
        throw new ValidationException([new ValidationFailure("Member", message)]);
}
