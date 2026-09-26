using IdentityService.Application.Queries.GetSetupStatus;
using IdentityService.Application.Commands.CompleteSetup;
using System.Security.Claims;
using BuildingBlocks.Web;
using IdentityService.API.Contracts;
using IdentityService.Application.Commands.AcceptInvitation;
using IdentityService.Application.Commands.ChangePassword;
using IdentityService.Application.Commands.CompletePasswordReset;
using IdentityService.Application.Commands.RefreshSession;
using IdentityService.Application.Commands.SignIn;
using IdentityService.Application.Commands.SignOut;
using IdentityService.Application.DTOs;
using IdentityService.Application.Queries.GetCurrentUser;
using IdentityService.Application.Queries.GetInvitationPreview;
using IdentityService.Application.Queries.GetPasswordResetPreview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IdentityService.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Exchanges an address and a password for two cookies. The body carries the user and no
    /// token: a token in a response body is a token in a browser's network log, in an error
    /// report, and in whatever the client decides to keep.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn(
        [FromBody] SignInRequest request,
        CancellationToken cancellationToken
    )
    {
        var session = await _sender.Send(
            new SignInCommand(request.Email, request.Password),
            cancellationToken
        );

        if (session is null)
            return Unauthorized(
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Sign-in failed",
                    // One sentence for three causes, on purpose. See SignInCommand.
                    Detail = "That address and password do not match an active account.",
                }
            );

        Issue(session);

        return Ok(session.User);
    }

    /// <summary>
    /// Trades the refresh cookie for a new pair. Anonymous because the access token it replaces is
    /// expected to have expired — requiring a valid one here would make the endpoint useless at
    /// exactly the moment it is needed.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var presented = Request.Cookies[PlatformCookies.RefreshToken];

        if (string.IsNullOrEmpty(presented))
            return Unauthorized(RefreshFailed());

        var session = await _sender.Send(
            new RefreshSessionCommand(presented),
            cancellationToken
        );

        if (session is null)
        {
            // Clear both cookies on the way out. Leaving a refresh cookie that the server has
            // already rejected means the console retries with it on every 401 it meets.
            Clear();

            return Unauthorized(RefreshFailed());
        }

        Issue(session);

        return Ok(session.User);
    }

    /// <summary>
    /// Ends this session. Anonymous and always 204: a sign-out that can fail is a sign-out the
    /// reader cannot trust, and the cookies are cleared either way.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        await _sender.Send(
            new SignOutCommand(Request.Cookies[PlatformCookies.RefreshToken]),
            cancellationToken
        );

        Clear();

        return NoContent();
    }

    /// <summary>
    /// Who the caller is, read from the database rather than from the token. The claims were true
    /// when the token was minted; a role change or a deactivation since then is only visible here.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
            return Unauthorized();

        var user = await _sender.Send(new GetCurrentUserQuery(userId), cancellationToken);

        if (user is null)
        {
            Clear();

            return Unauthorized();
        }

        return Ok(user);
    }

    // ---- one-time links: an invitation and a reset both end in a signed-in session ------------

    /// <summary>What the invitation offers, for the page that asks for a name and a password.</summary>
    [HttpGet("invitations/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Invitation(string token, CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new GetInvitationPreviewQuery(token), cancellationToken));

    /// <summary>
    /// Becomes an account in the invitation's organisation and signs it in. The organisation and the
    /// role are the invitation's; the body chooses only the name and the password.
    /// </summary>
    [HttpPost("invitations/{token}/accept")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvitation(
        string token,
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken
    )
    {
        var session = await _sender.Send(
            new AcceptInvitationCommand(token, request.DisplayName, request.Password),
            cancellationToken
        );

        Issue(session);

        return Ok(session.User);
    }

    [HttpGet("password-resets/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> PasswordReset(string token, CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new GetPasswordResetPreviewQuery(token), cancellationToken));

    /// <summary>Sets the new password, ends every other session, and signs this one in.</summary>
    [HttpPost("password-resets/{token}/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> CompletePasswordReset(
        string token,
        [FromBody] CompletePasswordResetRequest request,
        CancellationToken cancellationToken
    )
    {
        var session = await _sender.Send(new CompletePasswordResetCommand(token, request.Password), cancellationToken);

        Issue(session);

        return Ok(session.User);
    }

    /// <summary>One's own password, with the current one. Other sessions end; this one is renewed.</summary>
    [HttpPost("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
            return Unauthorized();

        var session = await _sender.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken
        );

        Issue(session);

        return Ok(session.User);
    }

    // ---- first-run setup (Adım 25) ---------------------------------------------------------------

    /// <summary>Whether this installation still needs its first Admin. Nothing more.</summary>
    [HttpGet("setup")]
    [AllowAnonymous]
    public async Task<IActionResult> SetupStatus(CancellationToken cancellationToken) =>
        Ok(new { required = await _sender.Send(new GetSetupStatusQuery(), cancellationToken) });

    /// <summary>
    /// Creates the organisation and its first Admin, with the one-time code from the service's
    /// log, and signs the Admin in. Runs once; every refusal is the same 404.
    /// </summary>
    [HttpPost("setup/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteSetup(
        [FromBody] CompleteSetupRequest request,
        CancellationToken cancellationToken
    )
    {
        var session = await _sender.Send(
            new CompleteSetupCommand(
                request.SetupCode,
                request.OrganizationName,
                request.DisplayName,
                request.Email,
                request.Password
            ),
            cancellationToken
        );

        Issue(session);

        return Ok(session.User);
    }

    public sealed record CompleteSetupRequest(
        string SetupCode,
        string OrganizationName,
        string DisplayName,
        string Email,
        string Password
    );

    public sealed record AcceptInvitationRequest(string DisplayName, string Password);

    public sealed record CompletePasswordResetRequest(string Password);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    private static ProblemDetails RefreshFailed() =>
        new()
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Session expired",
            Detail = "Sign in again.",
        };

    private void Issue(IssuedSession session)
    {
        Response.Cookies.Append(
            PlatformCookies.AccessToken,
            session.Access.Value,
            Options(session.Access.ExpiresAt, path: "/")
        );

        Response.Cookies.Append(
            PlatformCookies.RefreshToken,
            session.Refresh.Value,
            Options(session.Refresh.ExpiresAt, path: PlatformCookies.RefreshPath)
        );
    }

    private void Clear()
    {
        // Same path and flags as when it was written, or the browser keeps the original alongside
        // the deletion and nothing is actually cleared.
        Response.Cookies.Delete(
            PlatformCookies.AccessToken,
            new CookieOptions { Path = "/", HttpOnly = true, Secure = Secure, SameSite = SameSiteMode.Strict }
        );

        Response.Cookies.Delete(
            PlatformCookies.RefreshToken,
            new CookieOptions
            {
                Path = PlatformCookies.RefreshPath,
                HttpOnly = true,
                Secure = Secure,
                SameSite = SameSiteMode.Strict,
            }
        );
    }

    private CookieOptions Options(DateTime expiresAt, string path) =>
        new()
        {
            // Script cannot read it, which is the whole reason the session is not in localStorage.
            HttpOnly = true,

            // Never attached to a request that started on someone else's page, which is both the
            // CSRF answer and what makes the cookie fallback in AddPlatformAuth safe.
            SameSite = SameSiteMode.Strict,

            Secure = Secure,

            Path = path,

            // An expiry the browser can act on, so a closed laptop does not keep sending a value
            // the server stopped accepting a fortnight ago.
            Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero),
        };

    // Secure exactly when the browser used HTTPS. Behind the installer's TLS proxy and the gateway
    // that arrives as X-Forwarded-Proto (PlatformForwardedHeaders); over plain HTTP — development,
    // or trying an installation out on a LAN address — a Secure cookie would never be sent back
    // and nobody could sign in. It used to be "not localhost", which inside a container network,
    // where the gateway calls this service by its service name, meant always.
    private bool Secure => Request.IsHttps;
}
