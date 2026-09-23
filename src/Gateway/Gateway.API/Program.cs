using BuildingBlocks.Observability;
using Gateway.API;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformLogging(TelemetryConstants.ServiceNames.Gateway);

var (routes, clusters) = GatewayRoutes.Build(builder.Configuration);

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);

var app = builder.Build();

// No UseHttpsRedirection here, and it comes out of the four services in Adım 16. TLS terminates
// at this edge; a service behind it that redirects to https is redirecting a request that already
// arrived over a private hop, and in development it redirects a plain-HTTP call to a port nothing
// is listening on.
app.MapReverseProxy();

app.Run();
