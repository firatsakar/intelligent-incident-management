using BuildingBlocks.Outbox;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Sessions;
using IdentityService.Infrastructure.Outbox;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Repositories;
using IdentityService.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("IdentityDb");

        services.AddScoped<ConvertDomainEventsToOutboxInterceptor>();

        services.AddDbContext<IdentityDbContext>(
            (sp, options) =>
            {
                options.UseNpgsql(connectionString);
                options.AddInterceptors(
                    sp.GetRequiredService<ConvertDomainEventsToOutboxInterceptor>()
                );
            }
        );

        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // PlatformJwtOptions is registered by AddPlatformAuth, which every service including this
        // one calls; this project is the only one that also mints.
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();

        services.AddScoped<SessionIssuer>();

        // The outbox mechanics are shared; only the routing is ours.
        services.AddScoped<IOutboxStore, IdentityOutboxStore>();
        services.AddScoped<IOutboxMessageHandler, OrganizationCreatedOutboxHandler>();

        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<OutboxCleanupService>();

        services.AddHostedService<IdentitySeeder>();
        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }
}
