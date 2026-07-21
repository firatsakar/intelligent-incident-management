using AgentOrchestrator.Domain.Aggregates;

namespace AgentOrchestrator.Application.Abstractions;

public interface IAnalysisIndexer
{
    /// <summary>
    /// Makes a completed analysis searchable. Idempotent: re-indexing the same
    /// analysis overwrites the existing document rather than creating a duplicate.
    /// </summary>
    Task IndexAsync(IncidentAnalysis analysis, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk variant for backfill / reindex scenarios.
    /// </summary>
    Task IndexManyAsync(
        IReadOnlyCollection<IncidentAnalysis> analyses,
        CancellationToken cancellationToken = default
    );
}
