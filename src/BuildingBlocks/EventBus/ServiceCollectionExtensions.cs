using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BuildingBlocks.EventBus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration
            .GetSection(EventBusOptions.SectionName)
            .Get<EventBusOptions>() ?? new EventBusOptions();

        services.AddSingleton(options);

        services.AddSingleton<IConnectionFactory>( _ => 
           new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost
            });

        // Built by hand so the configured retry count reaches it. Left to the container, the
        // constructor's default of three was used and EventBus:RetryCount was read by nothing
        // (found in Adım 23).
        services.AddSingleton(sp => new RabbitMqConnection(
            sp.GetRequiredService<IConnectionFactory>(),
            sp.GetRequiredService<ILogger<RabbitMqConnection>>(),
            options.RetryCount));
        services.AddSingleton<IEventBus, RabbitMqEventBus>();

        return services;
    }
}
