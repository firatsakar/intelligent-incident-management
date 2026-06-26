using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Application.Abstractions;

public interface IAiAnalyzer
{
    Task<AnalysisResult> AnalyzeIncidentAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default
    );
}
