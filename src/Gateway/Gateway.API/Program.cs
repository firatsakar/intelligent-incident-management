using System.Threading.RateLimiting;
using BuildingBlocks.Observability;
using BuildingBlocks.SharedKernel;
using BuildingBlocks.Web;
using Gateway.API;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformLogging(TelemetryConstants.ServiceNames.Gateway);
builder.Services.AddPlatformTracing(builder.Configuration, TelemetryConstants.ServiceNames.Gateway);

var (routes, clusters) = GatewayRoutes.Build(builder.Configuration);

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);

builder.Services.AddRateLimiter(limiter =>
{
    // Per caller address, so one person guessing cannot lock everyone else out of signing in.
    // Ten a minute is far above anybody typing their own password wrong and far below anything
    // worth calling an attempt at guessing — and each rejected request is also a BCrypt
    // verification at work factor 12 that the server does not have to do.
    limiter.AddPolicy(
        GatewayRoutes.SignInRateLimiterPolicy,
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),

                    // No queue. A caller past the limit should be told so immediately rather than
                    // held open, which is the same reasoning as failing a sign-in fast.
                    QueueLimit = 0,
                }
            )
    );

    // Per key rather than per address: many senders can sit behind one address, and one sender's
    // loop should not spend another's allowance. Partitioned by the key's hash, so the limiter never
    // holds a key in the clear; a request without one falls back to its address.
    limiter.AddPolicy(
        GatewayRoutes.IncidentIntakeRateLimiterPolicy,
        context =>
        {
            var key = context.Request.Headers[GatewayRoutes.IncidentApiKeyHeader].ToString().Trim();

            var partition = key.Length > 0
                ? "key:" + AccessKey.Hash(key)
                : "address:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            return RateLimitPartition.GetFixedWindowLimiter(
                partition,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            );
        }
    );

    limiter.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata)
            ? metadata
            : TimeSpan.FromMinutes(1);

        context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many attempts",
                Detail = $"Wait {(int)retryAfter.TotalSeconds} seconds and try again.",
            },
            cancellationToken
        );
    };
});

var app = builder.Build();

var console = SpaHosting.TryResolve(app.Configuration, app.Logger);

// First of all: the static files, the rate limiter and the proxied services all read the caller's
// address and scheme, and behind the installer's TLS proxy those are what the proxy reports — but
// only a proxy named in configuration is believed (Adım 26).
var forwarded = PlatformForwardedHeaders.ForEdge(app.Configuration);

if (forwarded is not null)
{
    app.UseForwardedHeaders(forwarded);

    app.Logger.LogInformation(
        "Believing X-Forwarded-For and X-Forwarded-Proto from {Proxies}.",
        app.Configuration[PlatformForwardedHeaders.KnownProxiesConfigurationKey]
    );
}

// No UseHttpsRedirection here, and it comes out of the four older services in Parça 5. TLS
// terminates at this edge; a service behind it that redirects to https is redirecting a request
// that already arrived over a private hop, and in development it redirects a plain-HTTP call to a
// port nothing is listening on.

if (console is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = console.Files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = console.Files });
}

// Explicit, because the rate limiter reads its policy off the matched endpoint and therefore has
// to run after routing rather than wherever the host would otherwise insert it.
app.UseRouting();

app.UseMiddleware<CookieBearerMiddleware>();

app.UseRateLimiter();

app.MapReverseProxy();

if (console is not null)
{
    // Anything that is neither a route above nor a file on disk is a console URL like
    // /incidents/{id}, which exists only in the browser's router.
    app.MapFallbackToFile(
        "index.html",
        new StaticFileOptions { FileProvider = console.Files }
    );
}

app.Run();
