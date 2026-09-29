using IncidentService.Domain.Aggregates;

namespace IncidentService.Application.Abstractions;

public interface IIncidentApiKeyRepository
{
    /// <summary>The organisation's keys, newest first. Scoped by the organisation filter.</summary>
    Task<IReadOnlyList<IncidentApiKey>> ListAsync(CancellationToken cancellationToken = default);

    Task<IncidentApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// The key a request presents, found by its hash across every organisation — before a scope
    /// exists, because the key is what establishes it. Tracked, so its last use can be saved with
    /// whatever the request goes on to write. The only unscoped read of the table.
    /// </summary>
    Task<IncidentApiKey?> FindByHashForIntakeAsync(string keyHash, CancellationToken cancellationToken = default);

    Task AddAsync(IncidentApiKey key, CancellationToken cancellationToken = default);

    void Remove(IncidentApiKey key);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
