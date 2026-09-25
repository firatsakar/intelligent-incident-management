using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public sealed record NotificationDeliveryDto
{
    public required Guid Id { get; init; }
    public required Guid IntegrationId { get; init; }

    // Where it went, readable by every role: the integration list itself is Admin-only.
    public string? IntegrationName { get; init; }
    public NotificationChannelType? Channel { get; init; }

    public required Guid IncidentId { get; init; }
    public required Guid EventId { get; init; }
    public required DeliveryStatus Status { get; init; }
    public required int AttemptCount { get; init; }
    public string? LastError { get; init; }
    public DateTime? SentAt { get; init; }
    public required DateTime CreatedAt { get; init; }

    public static NotificationDeliveryDto FromDomain(NotificationDelivery delivery)
    {
        return new NotificationDeliveryDto
        {
            Id = delivery.Id,
            IntegrationId = delivery.IntegrationId,
            IntegrationName = delivery.IntegrationName,
            Channel = delivery.Channel,
            IncidentId = delivery.IncidentId,
            EventId = delivery.EventId,
            Status = delivery.Status,
            AttemptCount = delivery.AttemptCount,
            LastError = delivery.LastError,
            SentAt = delivery.SentAt,
            CreatedAt = delivery.CreatedAt,
        };
    }
}
