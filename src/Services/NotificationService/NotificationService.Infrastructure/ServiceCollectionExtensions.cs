using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Abstractions;
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

        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();

        return services;
    }
}
