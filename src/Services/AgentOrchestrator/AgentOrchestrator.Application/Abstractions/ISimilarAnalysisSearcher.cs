using AgentOrchestrator.Application.DTOs;

public interface ISimilarAnalysisSearcher
{
    Task<IReadOnlyList<SimilarAnalysis>> SearchAsync(
        string title,
        string description,
        Guid? excludeIncidentId = null,
        int maxResults = 3,
        CancellationToken cancellationToken = default
    );
}
