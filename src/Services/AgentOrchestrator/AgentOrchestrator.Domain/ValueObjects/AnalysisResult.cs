using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.ValueObjects;

public sealed record AnalysisResult : ValueObject
{
    public required string SuggestedPriority { get; init; }
    public required string SuggestedCategory { get; init; }
    public required string Reasoning { get; init; }
    public IReadOnlyList<string> SuggestedSteps { get; init; } = [];
    public double? Confidence { get; init; }
}
