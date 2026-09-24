using System.Text;
using BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Web;

/// <summary>The names both halves of the platform have to agree on.</summary>
public static class PlatformClaims
{
    /// <summary>The organisation every row the caller may see belongs to.</summary>
    public const string Organization = "org";

    /// <summary>Admin / Engineer / Viewer.</summary>
    public const string Role = "role";

    /// <summary>What to put in the corner of the screen. Never used to decide anything.</summary>
    public const string DisplayName = "name";
}

/// <summary>
/// The two cookies a session is carried in, and the path the second one is limited to.
/// </summary>
/// <remarks>
/// Cookies rather than a token in <c>localStorage</c>, which the gateway is what makes possible:
/// one origin means these can be <c>HttpOnly</c> and <c>SameSite=Strict</c>, so script cannot read
/// them and the browser will not send them from anyone else's page. That last part is also the
/// CSRF answer — a cookie that never travels cross-site cannot be used from one.
/// </remarks>
public static class PlatformCookies
{
    public const string AccessToken = "iim.access";

    public const string RefreshToken = "iim.refresh";

    /// <summary>
    /// The refresh cookie is scoped to the one endpoint that consumes it, so it is not attached to
    /// every request the console makes. The value that is sent constantly should be the one that
    /// expires in minutes, not the one that lasts a fortnight.
    /// </summary>
    public const string RefreshPath = "/api/auth/refresh";
}

/// <summary>How a token is signed and who it is for. One section, read by the issuer and by every validator.</summary>
public sealed class PlatformJwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC-SHA256, so at least 256 bits. There is no default and there must not be one: a signing
    /// key with a fallback is a signing key somebody else also knows.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "iim";

    public string Audience { get; set; } = "iim";

    /// <summary>Short, because it cannot be revoked. Revocation is the refresh token's job.</summary>
    public int AccessMinutes { get; set; } = 15;

    public int RefreshDays { get; set; } = 14;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class PlatformAuthentication
{
    /// <summary>
    /// Validates the platform's own access token. Every service calls this with the same
    /// configuration section, so a token minted by IdentityService is accepted everywhere and
    /// nowhere else.
    /// </summary>
    public static IServiceCollection AddPlatformAuth(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var options =
            configuration.GetSection(PlatformJwtOptions.SectionName).Get<PlatformJwtOptions>()
            ?? new PlatformJwtOptions();

        // Fail at startup rather than on the first sign-in. A service that boots and then rejects
        // every token looks like a token problem, and the person debugging it starts in the wrong
        // place.
        if (string.IsNullOrWhiteSpace(options.SigningKey))
            throw new InvalidOperationException(
                $"{PlatformJwtOptions.SectionName}:SigningKey is not configured. "
                    + "Set it in user secrets in development, or in the environment in production."
            );

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
            throw new InvalidOperationException(
                $"{PlatformJwtOptions.SectionName}:SigningKey is shorter than the 256 bits HMAC-SHA256 requires."
            );

        services.AddSingleton(options);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = options.Key(),
                    ValidateLifetime = true,

                    // The default five minutes of tolerance would keep a fifteen-minute token alive
                    // for twenty, which is a third of its life spent expired.
                    ClockSkew = TimeSpan.Zero,

                    RoleClaimType = PlatformClaims.Role,
                    NameClaimType = PlatformClaims.DisplayName,
                };

                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // The gateway turns the access cookie into an Authorization header, which
                        // is the normal path. This fallback is for a service reached directly and
                        // for the WebSocket handshake, where a browser cannot set a header at all.
                        // It is not a CSRF opening: the cookie is SameSite=Strict, so it is never
                        // attached to a request that started on somebody else's page.
                        if (string.IsNullOrEmpty(context.Token))
                            context.Token = context.Request.Cookies[PlatformCookies.AccessToken];

                        return Task.CompletedTask;
                    },
                };
            });

        // Requiring a token is the default and opting out is the exception. The alternative —
        // remembering [Authorize] on twenty-seven actions and on everything added later — fails
        // silently and in the direction that matters, by answering someone who never signed in.
        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddScoped<IOrganizationContext, OrganizationContext>();

        return services;
    }

    /// <summary>
    /// Fills <see cref="IOrganizationContext"/> from the authenticated caller's claim. Goes after
    /// <c>UseAuthentication</c>, which is what puts the claim there in the first place.
    /// </summary>
    public static IApplicationBuilder UseOrganizationContext(this IApplicationBuilder app) =>
        app.Use(
            (context, next) =>
            {
                var claim = context.User.FindFirst(PlatformClaims.Organization)?.Value;

                if (Guid.TryParse(claim, out var organizationId))
                    context.RequestServices.GetRequiredService<IOrganizationContext>()
                        .Set(organizationId);

                // No claim is not an error here. An anonymous endpoint is allowed to have no
                // organisation; what must not happen is a scoped query running without one, and
                // that is refused where the scope is read, not where it is missing.
                return next();
            }
        );
}
