using BuildingBlocks.SharedKernel;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Aggregates;

// One row per (integration, incident) notification attempt. The row is what makes the consumer
// idempotent: delivery is at-least-once, so a redelivered event must not send a second copy.
public sealed class NotificationDelivery : AggregateRoot
{
    // Matches the column width configured for LastError.
    private const int MaxErrorLength = 2048;

    private NotificationDelivery() { }

    /// <summary>
    /// Whose delivery this is. The integration it went through and the incident it was about both
    /// belong to one organisation already; carried here too because nothing in this model has a
    /// navigation to inherit it through, and the delivery-health screen reads this table directly.
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public Guid IntegrationId { get; private set; }

    /// <summary>
    /// What the integration was called, and which channel it was, when this went through it.
    /// </summary>
    /// <remarks>
    /// Copied onto the row (Adım 16.5) so the delivery history reads on its own. Integrations are
    /// the organisation's configuration and only its Admins may read them now; the incident screen
    /// every role opens still has to say where a notification went. Null for rows written before
    /// the column existed whose integration was already deleted by then.
    /// </remarks>
    public string? IntegrationName { get; private set; }

    public NotificationChannelType? Channel { get; private set; }

    public Guid IncidentId { get; private set; }

    // The integration event's Id, kept for tracing a delivery back to the message that caused it.
    public Guid EventId { get; private set; }

    public DeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? SentAt { get; private set; }

    public static NotificationDelivery Start(
        Guid organizationId,
        Guid integrationId,
        Guid incidentId,
        Guid eventId,
        string? integrationName = null,
        NotificationChannelType? channel = null
    )
    {
        return new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            IntegrationId = integrationId,
            IntegrationName = integrationName,
            Channel = channel,
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
        // Provider errors can be far longer than the column; a truncated reason beats a write
        // that fails and loses the record of the failure entirely.
        LastError = error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
        SetUpdatedAt();
    }
}
