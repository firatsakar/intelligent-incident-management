using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BuildingBlocks.Web;
using IncidentService.Application.Abstractions;

namespace IncidentService.API.Security;

/// <summary>
/// The signed-in person, read from the token on the current request. Outside a request — the
/// event bus, a hosted service — there is none, and both values are null.
/// </summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal =>
        _accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? Name => Principal?.FindFirstValue(PlatformClaims.DisplayName);
}
