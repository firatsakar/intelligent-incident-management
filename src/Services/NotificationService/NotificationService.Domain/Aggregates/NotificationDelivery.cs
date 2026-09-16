using BuildingBlocks.SharedKernel;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Aggregates;

// One row per (integration, incident) notification attempt. The row is what makes the consumer
// idempotent: delivery is at-least-once, so a redelivered event must not send a second copy.
public sealed class NotificationDelivery : AggregateRoot
{
    private NotificationDelivery() { }

    public Guid IntegrationId { get; private set; }
    public Guid IncidentId { get; private set; }

    // The integration event's Id, kept for tracing a delivery back to the message that caused it.
    public Guid EventId { get; private set; }

    public DeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? SentAt { get; private set; }

    public static NotificationDelivery Start(Guid integrationId, Guid incidentId, Guid eventId)
    {
        return new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            IntegrationId = integrationId,
            IncidentId = incidentId,
            EventId = eventId,
            Status = DeliveryStatus.Pending,
        };
    }

    public void MarkSent()
    {
        Status = DeliveryStatus.Sent;
        AttemptCount++;
        LastError = null;
        SentAt = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public void MarkFailed(string error)
    {
        Status = DeliveryStatus.Failed;
        AttemptCount++;
        LastError = error;
        SetUpdatedAt();
    }
}
