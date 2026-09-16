using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Channels;

public sealed class NotificationChannelResolver : INotificationChannelResolver
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationChannelResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public INotificationChannel Resolve(NotificationChannelType channel)
    {
        return _serviceProvider.GetRequiredKeyedService<INotificationChannel>(channel);
    }
}
