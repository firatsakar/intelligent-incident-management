using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Abstractions;

public interface ITelemetrySourceRepository
{
    Task<TelemetrySource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetrySource>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Every enabled source, across every organisation.
    /// </summary>
    /// <remarks>
    /// The one read in this service that deliberately crosses organisations, and it has its own
    /// method so that it is impossible to do by accident. The polling loop is woken by a timer
    /// rather than by anybody: it has no scope to run under, and it serves all of them. Whatever
    /// it finds is then polled inside a scope taken from the source's own row.
    /// </remarks>
    Task<IReadOnlyList<TelemetrySource>> GetEnabledForPollingAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// The source a pushed batch's key belongs to, whichever organisation that is.
    /// </summary>
    /// <remarks>
    /// Crosses organisations for the same reason polling does, and is named for it: a push arrives
    /// with a key and nothing else, so the organisation cannot be known until the row is found. The
    /// endpoint then scopes everything after this from the row it returns.
    /// </remarks>
    Task<TelemetrySource?> FindByIngestKeyHashForIngestAsync(
        string ingestKeyHash,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<TelemetrySource>> GetEnabledAsync(
        CancellationToken cancellationToken = default
    );
    Task AddAsync(TelemetrySource source, CancellationToken cancellationToken = default);
    void Update(TelemetrySource source);
    void Remove(TelemetrySource source);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
