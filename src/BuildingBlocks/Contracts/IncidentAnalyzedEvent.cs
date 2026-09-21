using BuildingBlocks.EventBus;

namespace BuildingBlocks.Contracts;

public sealed record IncidentAnalyzedEvent : IntegrationEvent
{
    public required Guid IncidentId { get; init; }
    public required string IncidentTitle { get; init; }
    public required string SuggestedPriority { get; init; }
    public required string SuggestedCategory { get; init; }
    public required string Reasoning { get; init; }
    public double? Confidence { get; init; }
}
