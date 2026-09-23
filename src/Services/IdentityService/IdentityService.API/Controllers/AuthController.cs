using System.Security.Claims;
using BuildingBlocks.Web;
using IdentityService.API.Contracts;
using IdentityService.Application.Commands.RefreshSession;
using IdentityService.Application.Commands.SignIn;
using IdentityService.Application.Commands.SignOut;
using IdentityService.Application.DTOs;
using IdentityService.Application.Queries.GetCurrentUser;
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

    // Secure would make these cookies invisible over plain HTTP, which is how the gateway is
    // reached in development. In production TLS terminates at the gateway and the flag is on.
    private bool Secure => !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
}
