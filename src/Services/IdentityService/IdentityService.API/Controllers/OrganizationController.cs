using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BuildingBlocks.Web;
using IdentityService.Application.Commands.ChangeMemberRole;
using IdentityService.Application.Commands.InviteMember;
using IdentityService.Application.Commands.IssuePasswordReset;
using IdentityService.Application.Commands.RevokeInvitation;
using IdentityService.Application.Queries.GetInvitations;
using IdentityService.Application.Commands.SetMemberActive;
using IdentityService.Application.Queries.GetMembers;
using IdentityService.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

/// <summary>
/// The organisation's own membership: who is in it and as what. Its Admins only, reads included —
/// the member list is the organisation's, like its integrations and its telemetry sources.
/// </summary>
[ApiController]
[Route("api/organization")]
[Authorize(Policy = PlatformPolicies.Administer)]
public sealed class OrganizationController : ControllerBase
{
    private readonly ISender _sender;

    public OrganizationController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("members")]
    public async Task<IActionResult> Members(CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new GetMembersQuery(), cancellationToken));

    [HttpPatch("members/{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        [FromBody] ChangeRoleRequest request,
        CancellationToken cancellationToken
    ) => Ok(await _sender.Send(new ChangeMemberRoleCommand(Actor(), id, request.Role), cancellationToken));

    [HttpPost("members/{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new SetMemberActiveCommand(Actor(), id, IsActive: false), cancellationToken));

    [HttpPost("members/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new SetMemberActiveCommand(Actor(), id, IsActive: true), cancellationToken));

    [HttpPost("members/{id:guid}/password-reset")]
    public async Task<IActionResult> IssuePasswordReset(Guid id, CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new IssuePasswordResetCommand(Actor(), id), cancellationToken));

    // The link is in this response and in the email, and nowhere else, ever.
    [HttpPost("invitations")]
    public async Task<IActionResult> Invite([FromBody] InviteRequest request, CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new InviteMemberCommand(Actor(), request.Email, request.Role), cancellationToken));

    [HttpGet("invitations")]
    public async Task<IActionResult> Invitations(CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new GetInvitationsQuery(), cancellationToken));

    [HttpPost("invitations/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new RevokeInvitationCommand(id), cancellationToken);

        return NoContent();
    }

    // The Administer policy already required an authenticated Admin, so a subject is present; a
    // token without one would be malformed rather than anonymous.
    private Guid Actor() =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new InvalidOperationException("An authenticated caller without a subject claim.");

    public sealed record ChangeRoleRequest(UserRole Role);

    public sealed record InviteRequest(string Email, UserRole Role);
}
