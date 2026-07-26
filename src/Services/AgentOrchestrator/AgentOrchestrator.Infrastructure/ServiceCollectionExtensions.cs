using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Infrastructure.Ai;
using AgentOrchestrator.Infrastructure.Outbox;
using AgentOrchestrator.Infrastructure.Persistence;
using AgentOrchestrator.Infrastructure.Persistence.Repositories;
using AgentOrchestrator.Infrastructure.Search;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentOrchestrator.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("AgentDb");
        services.AddDbContext<AgentDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IIncidentAnalysisRepository, IncidentAnalysisRepository>();

        services.Configure<AiAnalyzerOptions>(
            configuration.GetSection(AiAnalyzerOptions.SectionName)
        );

        services.AddSingleton<IAiAnalyzer, MafAiAnalyzer>();
        services.AddSingleton<ISimilarAnalysisSearcher, ElasticsearchSimilarAnalysisSearcher>();
        services.Configure<ElasticsearchOptions>(
            configuration.GetSection(ElasticsearchOptions.SectionName)
        );

        services.AddSingleton<ElasticsearchClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;

            var settings = new ElasticsearchClientSettings(new Uri(options.Uri)).DefaultIndex(
                options.AnalysesIndexName
            );

            return new ElasticsearchClient(settings);
        });

        services.AddHostedService<ElasticsearchConnectionCheck>();
        services.AddHostedService<ElasticsearchIndexInitializer>();
        services.AddSingleton<IAnalysisIndexer, ElasticsearchAnalysisIndexer>();

        services.AddScoped<ConvertDomainEventsToOutboxInterceptor>();

        services.AddDbContext<AgentDbContext>(
            (sp, options) =>
            {
                options.UseNpgsql();
                options.AddInterceptors(
                    sp.GetRequiredService<ConvertDomainEventsToOutboxInterceptor>()
                );
            }
        );

        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
