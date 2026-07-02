using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.ValueObjects;

public sealed record AnalysisMetadata : ValueObject
{
    public required string ModelName { get; init; }
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }
    public required long EndToEndDurationMs { get; init; }

    public int TotalTokens => InputTokens + OutputTokens;
}
