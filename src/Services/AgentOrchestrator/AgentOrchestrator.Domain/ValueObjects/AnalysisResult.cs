using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.ValueObjects;

public sealed record AnalysisResult : ValueObject
{
    public required string SuggestedPriority { get; init; }
    public required string SuggestedCategory { get; init; }
    public required string Reasoning { get; init; }
    public IReadOnlyList<string> SuggestedSteps { get; init; } = [];
    public double? Confidence { get; init; }

    // Commits the analysis named as likely causes (Adım 17.5). Empty when it read no code or found
    // nothing that explained the incident — which is most of the time, and not a failure.
    public IReadOnlyList<RelatedChange> RelatedChanges { get; init; } = [];
    public AnalysisMetadata? Metadata { get; init; }
}
