using Microsoft.Extensions.DependencyInjection.Extensions;
using BuildingBlocks.Outbox;
using BuildingBlocks.SharedKernel;
using IncidentService.Application.Abstractions;
using IncidentService.Infrastructure.Outbox;
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

        // Registered here as well as by AddPlatformAuth: the three event consumers run in bus
        // scopes, and the context has to exist there for the bus to fill it in.
        services.TryAddScoped<IOrganizationContext, OrganizationContext>();

        services.AddScoped<ConvertDomainEventsToOutboxInterceptor>();

        // A closed incident and the message that tells telemetry about it commit in the same
        // transaction (Adım 24). Until then this service published straight after SaveChanges,
        // which it still does for IncidentDetectedEvent — see PROGRESS.md tech-debt.
        services.AddDbContext<IncidentDbContext>(
            (sp, options) =>
            {
                options.UseNpgsql(connectionString);
                options.AddInterceptors(
                    sp.GetRequiredService<ConvertDomainEventsToOutboxInterceptor>()
                );
            }
        );

        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IIncidentApiKeyRepository, IncidentApiKeyRepository>();

        services.AddScoped<IOutboxStore, IncidentOutboxStore>();
        services.AddScoped<IOutboxMessageHandler, IncidentResolvedOutboxHandler>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<OutboxCleanupService>();

        // Registered unconditionally; the endpoint that uses it checks the environment itself, so
        // the gate lives in one obvious place rather than being split across two files.
        services.AddScoped<DemoIncidentSeeder>();

        return services;
    }
}