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

    // Commits the analysis named as likely causes (Adım 17.5), empty when it read no code or found
    // nothing. Every field was written by the platform from what GitHub returned — Url included.
    public IReadOnlyList<AnalysisRelatedChange> RelatedChanges { get; init; } = [];
}

public sealed record AnalysisRelatedChange(
    string Sha,
    string Title,
    string? Author,
    DateTime CommittedAt,
    string Url
);
