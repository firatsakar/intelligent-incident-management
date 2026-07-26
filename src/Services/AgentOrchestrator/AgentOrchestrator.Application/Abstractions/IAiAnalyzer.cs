using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Application.Abstractions;

public interface IAiAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(
        Guid incidentId,
        string title,
        string description,
        CancellationToken cancellationToken = default
    );
}
