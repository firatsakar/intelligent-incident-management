using BuildingBlocks.Application.Behaviors;
using FluentValidation;
using MediatR;
using BuildingBlocks.Web;
using System.Text.Json.Serialization;
using AgentOrchestrator.API.BackgroundServices;
using AgentOrchestrator.Application.Commands.AnalyzeIncident;
using AgentOrchestrator.Application.EventHandlers;
using AgentOrchestrator.Infrastructure;
using AgentOrchestrator.Infrastructure.Ai;
using AgentOrchestrator.Infrastructure.Persistence;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformLogging(TelemetryConstants.ServiceNames.AgentOrchestrator);
builder.Services.AddPlatformTracing(builder.Configuration, TelemetryConstants.ServiceNames.AgentOrchestrator);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(AnalyzeIncidentCommand).Assembly)
);

// The settings endpoints (Adım 17.5) are the first here to take input from a form, so this is
// the first time the service needs the validation pipeline and the problem-details mapping the
// others have had since Adım 13.
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssembly(typeof(AnalyzeIncidentCommand).Assembly);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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

await app.MigrateOnStartupAsync<AgentDbContext>();

// Without a key the service still runs: the model refuses each call, the analysis is marked failed
// on its incident and not retried. Said once here, so an installation missing it finds out from
// the first lines of the log rather than from the first incident (Adım 26).
if (string.IsNullOrWhiteSpace(app.Configuration[$"{AiAnalyzerOptions.SectionName}:{nameof(AiAnalyzerOptions.ApiKey)}"]))
{
    app.Logger.LogWarning(
        "{Section}:{Key} is not set: every analysis will be marked failed until it is.",
        AiAnalyzerOptions.SectionName,
        nameof(AiAnalyzerOptions.ApiKey)
    );
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection. TLS terminates at the gateway; a service behind it redirecting
// to https is redirecting a request that already arrived over a private hop, and in
// development it redirects a plain-HTTP call to a port nothing is listening on.
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseOrganizationContext();
app.MapControllers();

app.Run();
