using System.Text.Json.Serialization;
using AgentOrchestrator.API.BackgroundServices;
using AgentOrchestrator.Application.Commands.AnalyzeIncident;
using AgentOrchestrator.Application.EventHandlers;
using AgentOrchestrator.Infrastructure;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
