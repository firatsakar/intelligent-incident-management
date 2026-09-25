using BuildingBlocks.Web;
using Microsoft.Net.Http.Headers;

namespace Gateway.API;

/// <summary>
/// Turns the access cookie into an <c>Authorization</c> header on the way through.
/// </summary>
/// <remarks>
/// <para>
/// The one place the browser's world and the services' world meet. A browser can hold an
/// <c>HttpOnly</c> cookie and cannot set a header from script; a service validating a JWT wants a
/// header and has no business knowing that cookies exist. Neither side has to bend, because this
/// middleware is three lines in the middle.
/// </para>
/// <para>
/// This is also what the whole single-origin argument was for. Without it the console would have
/// to keep the token somewhere script can read, which means somewhere an injected script can read.
/// </para>
/// </remarks>
internal sealed class CookieBearerMiddleware
{
    private readonly RequestDelegate _next;

    public CookieBearerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        // A caller that sent its own Authorization header keeps it. The downstream service
        // validates the token either way, so there is nothing to defend here, and a script or an
        // integration holding a bearer should go on working through the same front door as the
        // console.
        if (!context.Request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            var token = context.Request.Cookies[PlatformCookies.AccessToken];

            if (!string.IsNullOrEmpty(token))
                context.Request.Headers.Authorization = $"Bearer {token}";
        }

        return _next(context);
    }
}
