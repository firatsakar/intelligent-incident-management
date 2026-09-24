using System.Text.Json.Serialization;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.EventBus;
using BuildingBlocks.Observability;
using BuildingBlocks.Web;
using FluentValidation;
using TelemetryIngestionService.API.Realtime;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.CreateTelemetrySource;
using TelemetryIngestionService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformLogging(TelemetryConstants.ServiceNames.TelemetryIngestionService);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateTelemetrySourceCommand).Assembly)
);

builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssembly(typeof(CreateTelemetrySourceCommand).Assembly);

builder.Services.AddInfrastructure(builder.Configuration);

// The outbox publishes promotions onto the bus; nothing is consumed here yet.
builder.Services.AddRabbitMqEventBus(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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

builder.Services.AddSignalR();

builder.Services.AddSingleton<IRealtimeNotifier, SignalRSignalNotifier>();

// The same call IdentityService makes. Every service validates the token on its own:
// the gateway forwards it, it does not vouch for it.
builder.Services.AddPlatformAuth(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

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
app.MapHub<SignalHub>("/hubs/signals");

app.Run();
