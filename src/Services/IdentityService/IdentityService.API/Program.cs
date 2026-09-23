using System.Text.Json.Serialization;
using BuildingBlocks.Observability;
using BuildingBlocks.Web;
using IdentityService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformLogging(TelemetryConstants.ServiceNames.IdentityService);

builder.Services.AddInfrastructure(builder.Configuration);

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
app.MapControllers();

app.Run();
