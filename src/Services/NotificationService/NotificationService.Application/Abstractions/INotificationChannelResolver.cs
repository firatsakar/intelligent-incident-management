using NotificationService.Domain.Enums;

namespace NotificationService.Application.Abstractions;

// Keeps keyed-DI resolution out of the Application layer: handlers ask for a channel by type and
// Infrastructure decides how that lookup happens.
public interface INotificationChannelResolver
{
    INotificationChannel Resolve(NotificationChannelType channel);
}
