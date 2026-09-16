using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Infrastructure.Connectors;
using TelemetryIngestionService.Infrastructure.Persistence;
using TelemetryIngestionService.Infrastructure.Persistence.Repositories;

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

        services.AddScoped<ITelemetrySourceRepository, TelemetrySourceRepository>();
        services.AddScoped<ISourceCursorRepository, SourceCursorRepository>();

        // Keyed by source kind so the poller can resolve a connector straight from the source row.
        services.AddHttpClient(SeqTelemetryConnector.HttpClientName);

        services.AddKeyedScoped<ITelemetrySourceConnector, SeqTelemetryConnector>(
            TelemetrySourceKind.Seq
        );

        services.AddScoped<ITelemetrySourceConnectorResolver, TelemetrySourceConnectorResolver>();

        return services;
    }
}
