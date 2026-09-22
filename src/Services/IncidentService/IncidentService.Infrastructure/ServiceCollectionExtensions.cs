using IncidentService.Application.Abstractions;
using IncidentService.Infrastructure.Persistence;
using IncidentService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IncidentService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IncidentDb");

        services.AddDbContext<IncidentDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IIncidentRepository, IncidentRepository>();

        // Registered unconditionally; the endpoint that uses it checks the environment itself, so
        // the gate lives in one obvious place rather than being split across two files.
        services.AddScoped<DemoIncidentSeeder>();

        return services;
    }
}