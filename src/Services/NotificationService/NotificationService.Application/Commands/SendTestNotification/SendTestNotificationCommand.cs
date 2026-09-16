using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Commands.SendTestNotification;

// Sends a sample notification through one integration so a customer can prove their settings
// work before an incident depends on them. Returns null when the integration does not exist.
public sealed record SendTestNotificationCommand(Guid IntegrationId) : IRequest<DeliveryResult?>;
