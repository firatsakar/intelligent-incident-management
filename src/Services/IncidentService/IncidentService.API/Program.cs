using BuildingBlocks.EventBus;
using IncidentService.Application.Commands.CreateIncident;
using IncidentService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(CreateIncidentCommand).Assembly));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRabbitMqEventBus(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();