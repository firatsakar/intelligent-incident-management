using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Abstractions;

// One implementation per channel, resolved by NotificationChannelType through keyed DI. The
// integration carries the customer's settings, so a channel holds no per-customer state itself.
public interface INotificationChannel
{
    NotificationChannelType Channel { get; }

    Task<DeliveryResult> SendAsync(
        NotificationMessage message,
        Integration integration,
        CancellationToken cancellationToken = default
    );
}
