using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;

namespace IdentityService.Application.DTOs;

public sealed record InvitationDto(Guid Id, string Email, UserRole Role, DateTime ExpiresAt, DateTime CreatedAt)
{
    public static InvitationDto FromDomain(Invitation invitation) =>
        new(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAt, invitation.CreatedAt);
}

/// <summary>
/// The link, in the one response that issues it. Only its hash is stored, so this is the only
/// moment it can be shown — and whether the email went out, so the Admin knows to pass it on
/// themselves when it did not.
/// </summary>
public sealed record IssuedLinkDto(string Link, bool EmailSent, DateTime ExpiresAt);

public sealed record InvitationIssuedDto(InvitationDto Invitation, IssuedLinkDto Issued);

/// <summary>What the person opening an invitation sees before they have an account.</summary>
public sealed record InvitationPreviewDto(string OrganizationName, string Email, UserRole Role, DateTime ExpiresAt);

public sealed record PasswordResetPreviewDto(string Email, string DisplayName, DateTime ExpiresAt);
