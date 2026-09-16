using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelemetryIngestionService.Infrastructure.Persistence;

namespace TelemetryIngestionService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("TelemetryDb");

        services.AddDbContext<TelemetryDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
