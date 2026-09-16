using MediatR;

namespace NotificationService.Application.Commands.DispatchNotifications;

public sealed record DispatchNotificationsCommand : IRequest
{
    // The integration event's Id. Stable across redelivery, so it identifies the message that
    // caused a delivery.
    public required Guid EventId { get; init; }

    public required Guid IncidentId { get; init; }
    public required string IncidentTitle { get; init; }
    public required string SuggestedPriority { get; init; }
    public required string SuggestedCategory { get; init; }
    public required string Reasoning { get; init; }
    public double? Confidence { get; init; }
    public required DateTime AnalyzedAt { get; init; }
}
