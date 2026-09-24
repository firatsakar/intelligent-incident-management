using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Application.Abstractions;

public interface IAiAnalyzer
{
    /// <param name="organizationId">
    /// Whose incident this is. Never shown to the model: it goes into the search tool's closure,
    /// alongside the incident id the tool already excludes, so the model can search its own
    /// hypothesis but cannot widen the search past the organisation it is working for.
    /// </param>
    Task<AnalysisResult> AnalyzeAsync(
        Guid organizationId,
        Guid incidentId,
        string title,
        string description,
        CancellationToken cancellationToken = default
    );
}
