using System.Text.Json.Serialization;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Web;
using FluentValidation;
using NotificationService.API.BackgroundServices;
using NotificationService.Application.Commands.SendTestNotification;
using NotificationService.Application.EventHandlers;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(SendTestNotificationCommand).Assembly)
);

builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssembly(typeof(SendTestNotificationCommand).Assembly);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRabbitMqEventBus(builder.Configuration);

builder.Services.AddScoped<
    IIntegrationEventHandler<IncidentAnalyzedEvent>,
    IncidentAnalyzedEventHandler
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

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
