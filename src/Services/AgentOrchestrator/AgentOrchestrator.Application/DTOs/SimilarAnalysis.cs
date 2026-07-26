namespace AgentOrchestrator.Application.DTOs;

public sealed record SimilarAnalysis
{
    public required Guid IncidentId { get; init; }
    public required string Title { get; init; }
    public required string SuggestedCategory { get; init; }
    public required string SuggestedPriority { get; init; }
    public required string Reasoning { get; init; }
    public IReadOnlyList<string> SuggestedSteps { get; init; } = [];
    public required double MatchScore { get; init; }
    public required double Confidence { get; init; }
    public required DateTimeOffset AnalyzedAt { get; init; }
}
