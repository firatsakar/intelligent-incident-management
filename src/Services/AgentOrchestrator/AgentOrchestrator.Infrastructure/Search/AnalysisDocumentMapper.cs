using AgentOrchestrator.Domain.Aggregates;

namespace AgentOrchestrator.Infrastructure.Search;

internal static class AnalysisDocumentMapper
{
    public static AnalysisDocument ToDocument(IncidentAnalysis analysis) =>
        new()
        {
            IncidentId = analysis.IncidentId,
            Title = analysis.IncidentTitle,
            Description = analysis.IncidentDescription,
            SuggestedCategory = analysis.Result?.SuggestedCategory,
            SuggestedPriority = analysis.Result?.SuggestedPriority,
            Reasoning = analysis.Result?.Reasoning,
            SuggestedSteps = analysis.Result?.SuggestedSteps ?? [],
            Confidence = analysis.Result?.Confidence,
            AnalyzedAt = analysis.CompletedAt ?? DateTimeOffset.UtcNow,
        };
}
