using System.Text.Json.Serialization;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.EventBus;
using BuildingBlocks.Observability;
using BuildingBlocks.Web;
using FluentValidation;
using IdentityService.Application.Commands.SignIn;
using IdentityService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformLogging(TelemetryConstants.ServiceNames.IdentityService);
builder.Services.AddPlatformTracing(builder.Configuration, TelemetryConstants.ServiceNames.IdentityService);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SignInCommand).Assembly));

builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssembly(typeof(SignInCommand).Assembly);

builder.Services.AddInfrastructure(builder.Configuration);

// Publishes only. OrganizationCreatedEvent leaves through the outbox; nothing here subscribes, so
// this service never declares a queue of its own.
builder.Services.AddRabbitMqEventBus(builder.Configuration);

// This service both mints and validates. Validating its own tokens is what lets /api/auth/me
// answer, and it is the same call the other four services make in Parça 5.
builder.Services.AddPlatformAuth(builder.Configuration);

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

// No UseHttpsRedirection: this service is reached through the gateway, which is where TLS
// terminates. The four older services still have the line and lose it in Parça 5.
app.UseAuthentication();
app.UseAuthorization();

// Until Adım 16.5 nothing here was scoped to an organisation — sign-in comes before one is known.
// The members endpoints are, and they read it from the Admin's claim like every other service.
app.UseOrganizationContext();

app.MapControllers();

app.Run();
