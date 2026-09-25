using AgentOrchestrator.Application.DTOs;

public interface ISimilarAnalysisSearcher
{
    /// <param name="organizationId">
    /// Required, and applied as a filter rather than a scoring clause: another organisation's
    /// analysis must not be ranked low, it must not be a candidate at all.
    /// </param>
    Task<IReadOnlyList<SimilarAnalysis>> SearchAsync(
        string query,
        Guid organizationId,
        Guid? excludeIncidentId = null,
        int maxResults = 3,
        CancellationToken cancellationToken = default
    );
}
