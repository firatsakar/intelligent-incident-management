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
    /// <summary>
    /// Every completed analysis across every organisation, for rebuilding the search index.
    /// </summary>
    /// <remarks>
    /// The one read in this service that crosses organisations, named so it cannot be done by
    /// accident. A reindex rebuilds a derived view of the whole table; there is nobody behind it
    /// to scope it to, and a reindex that saw one organisation would silently delete the others
    /// from search. Scoping happens where the index is read, not where it is written.
    /// </remarks>
    Task<IReadOnlyCollection<IncidentAnalysis>> GetAllCompletedForReindexAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<IncidentAnalysis>> GetCompletedAsync(
        CancellationToken cancellationToken = default
    );
}
