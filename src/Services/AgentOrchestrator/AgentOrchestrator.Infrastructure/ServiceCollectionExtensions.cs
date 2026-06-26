using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Infrastructure.Ai;
using AgentOrchestrator.Infrastructure.Persistence;
using AgentOrchestrator.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddSingleton<IAiAnalyzer, AnthropicAiAnalyzer>();

        return services;
    }
}
