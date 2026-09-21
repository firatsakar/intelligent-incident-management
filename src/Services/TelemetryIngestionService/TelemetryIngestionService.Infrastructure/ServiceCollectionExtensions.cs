using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Infrastructure.Outbox;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Infrastructure.Connectors;
using TelemetryIngestionService.Infrastructure.Ingestion;
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

        services.AddScoped<ConvertDomainEventsToOutboxInterceptor>();

        services.AddDbContext<TelemetryDbContext>(
            (sp, options) =>
            {
                options.UseNpgsql(connectionString);
                // Promotions and their outbox messages commit in the same transaction, so a
                // signal can never be promoted without the message that tells anyone about it.
                options.AddInterceptors(
                    sp.GetRequiredService<ConvertDomainEventsToOutboxInterceptor>()
                );
            }
        );

        services.AddScoped<ITelemetrySourceRepository, TelemetrySourceRepository>();
        services.AddScoped<ISourceCursorRepository, SourceCursorRepository>();
        services.AddScoped<ILogRecordRepository, LogRecordRepository>();
        services.AddScoped<IErrorSignatureRepository, ErrorSignatureRepository>();
        services.AddScoped<IDetectionRuleRepository, DetectionRuleRepository>();
        services.AddScoped<ISignalRepository, SignalRepository>();

        // Keyed by source kind so the poller can resolve a connector straight from the source row.
        services.AddHttpClient(SeqTelemetryConnector.HttpClientName);

        services.AddKeyedScoped<ITelemetrySourceConnector, SeqTelemetryConnector>(
            TelemetrySourceKind.Seq
        );

        services.AddScoped<ITelemetrySourceConnectorResolver, TelemetrySourceConnectorResolver>();

        services.AddScoped<IOutboxStore, TelemetryOutboxStore>();
        services.AddScoped<IOutboxMessageHandler, SignalPromotedOutboxHandler>();

        services.AddHostedService<DetectionRuleSeeder>();
        services.AddHostedService<TelemetryPollingService>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<OutboxCleanupService>();

        return services;
    }
}
