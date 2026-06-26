using AgentOrchestrator.Domain.Aggregates;

namespace AgentOrchestrator.Application.Abstractions;

public interface IIncidentAnalysisRepository
{
    Task<IncidentAnalysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IncidentAnalysis?> GetByIncidentIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(IncidentAnalysis analysis, CancellationToken cancellationToken = default);
    void Update(IncidentAnalysis analysis);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
