using BuildingBlocks.Web;
using System.Text.Json.Serialization;
using AgentOrchestrator.API.BackgroundServices;
using AgentOrchestrator.Application.Commands.AnalyzeIncident;
using AgentOrchestrator.Application.EventHandlers;
using AgentOrchestrator.Infrastructure;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformLogging(TelemetryConstants.ServiceNames.AgentOrchestrator);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(AnalyzeIncidentCommand).Assembly)
);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRabbitMqEventBus(builder.Configuration);

builder.Services.AddScoped<
    IIntegrationEventHandler<IncidentDetectedEvent>,
    IncidentDetectedEventHandler
>();

builder.Services.AddHostedService<EventBusSubscriber>();

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// The same call IdentityService makes. Every service validates the token on its own:
// the gateway forwards it, it does not vouch for it.
builder.Services.AddPlatformAuth(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection. TLS terminates at the gateway; a service behind it redirecting
// to https is redirecting a request that already arrived over a private hop, and in
// development it redirects a plain-HTTP call to a port nothing is listening on.
app.UseAuthentication();
app.UseAuthorization();
app.UseOrganizationContext();
app.MapControllers();

app.Run();
