using BuildingBlocks.Outbox;
using IdentityService.Application.Abstractions;
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

        // The outbox mechanics are shared; the routing arrives with the handler in Parça 3.
        services.AddScoped<IOutboxStore, IdentityOutboxStore>();

        services.AddHostedService<IdentitySeeder>();
        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }
}
