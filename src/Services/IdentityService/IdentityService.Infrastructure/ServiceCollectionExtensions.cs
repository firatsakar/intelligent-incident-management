using IdentityService.Application.Setup;
using BuildingBlocks.Outbox;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Sessions;
using IdentityService.Infrastructure.Outbox;
using IdentityService.Infrastructure.Email;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Repositories;
using IdentityService.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BuildingBlocks.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("IdentityDb");

        // Registered here as well as in AddPlatformAuth, because a background scope has no HTTP
        // pipeline to have registered it: the outbox interceptor runs in whatever scope saved the
        // aggregate, and several of those are opened by a hosted service. TryAdd, so the two
        // registrations cannot become two different lifetimes.
        services.TryAddScoped<IOrganizationContext, OrganizationContext>();

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
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();

        // The platform's own mail — invitations and reset links — and the console address the
        // links in it point at.
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
        services.Configure<ConsoleOptions>(configuration.GetSection(ConsoleOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IConsoleLinks, ConsoleLinks>();

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

        // One per process: the seeder issues it on an empty database, the setup command spends it.
        services.AddSingleton<SetupCode>();
        services.AddHostedService<IdentitySeeder>();
        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }
}
