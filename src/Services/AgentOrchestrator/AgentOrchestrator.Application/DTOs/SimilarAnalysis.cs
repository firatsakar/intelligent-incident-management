namespace AgentOrchestrator.Application.DTOs;

public sealed record SimilarAnalysis
{
    public required Guid IncidentId { get; init; }
    public required string Title { get; init; }
    public required string SuggestedCategory { get; init; }
    public required string SuggestedPriority { get; init; }
    public required string Reasoning { get; init; }
    public IReadOnlyList<string> SuggestedSteps { get; init; } = [];
    public int MatchScore { get; init; }
}
