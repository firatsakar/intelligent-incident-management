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

        return services;
    }
}