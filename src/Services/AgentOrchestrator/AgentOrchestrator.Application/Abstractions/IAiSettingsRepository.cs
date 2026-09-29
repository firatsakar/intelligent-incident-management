using AgentOrchestrator.Domain.Aggregates;

namespace AgentOrchestrator.Application.Abstractions;

/// <summary>
/// The organisation's AI settings — at most one row, found through the organisation filter, so
/// there is no id to ask for and no way to ask for another organisation's.
/// </summary>
public interface IAiSettingsRepository
{
    Task<AiSettings?> GetAsync(CancellationToken cancellationToken = default);

    Task AddAsync(AiSettings settings, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
