using System.Text.Json.Serialization;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Observability;
using BuildingBlocks.Web;
using IncidentService.API.Realtime;
using FluentValidation;
using IncidentService.API.BackgroundServices;
using IncidentService.Application.Abstractions;
using IncidentService.Application.Commands.CreateIncident;
using IncidentService.Application.EventHandlers;
using IncidentService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformLogging(TelemetryConstants.ServiceNames.IncidentService);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateIncidentCommand).Assembly)
);

builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssembly(typeof(CreateIncidentCommand).Assembly);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRabbitMqEventBus(builder.Configuration);

builder.Services.AddScoped<
    IIntegrationEventHandler<IncidentAnalyzedEvent>,
    IncidentAnalyzedEventHandler
>();

builder.Services.AddScoped<
    IIntegrationEventHandler<SignalPromotedEvent>,
    SignalPromotedEventHandler
>();

builder.Services.AddScoped<
    IIntegrationEventHandler<IncidentAnalysisFailedEvent>,
    IncidentAnalysisFailedEventHandler
>();

builder.Services.AddHostedService<EventBusSubscriber>();

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

builder.Services.AddSingleton<IRealtimeNotifier, SignalRIncidentNotifier>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<IncidentHub>("/hubs/incidents");

app.Run();
