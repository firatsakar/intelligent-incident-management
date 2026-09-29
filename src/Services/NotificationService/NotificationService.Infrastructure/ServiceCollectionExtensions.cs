using Microsoft.Extensions.DependencyInjection.Extensions;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Channels;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Persistence.Repositories;

namespace NotificationService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("NotificationDb");

        // Registered here as well as by AddPlatformAuth: the dispatcher runs in a bus scope, and
        // the context has to exist there for the bus to fill it in.
        services.TryAddScoped<IOrganizationContext, OrganizationContext>();

        // The key that encrypts customer credentials at rest. Resolved when the first
        // context is built, so a service without it fails on its first query with the reason.
        services.AddSingleton(_ => SecretProtector.FromConfiguration(configuration[SecretProtector.ConfigurationKey]));

        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();

        // Keyed by channel type so the dispatcher can resolve the right channel straight from
        // the integration row.
        services.AddKeyedScoped<INotificationChannel, EmailNotificationChannel>(
            NotificationChannelType.Email
        );

        services.AddHttpClient(WebhookNotificationChannel.HttpClientName);

        services.AddKeyedScoped<INotificationChannel, WebhookNotificationChannel>(
            NotificationChannelType.Webhook
        );

        services.AddHttpClient(JiraNotificationChannel.HttpClientName);

        services.AddKeyedScoped<INotificationChannel, JiraNotificationChannel>(
            NotificationChannelType.Jira
        );

        services.AddScoped<INotificationChannelResolver, NotificationChannelResolver>();

        return services;
    }
}
