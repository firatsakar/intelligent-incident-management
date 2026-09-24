using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Abstractions;

public interface ISourceCursorRepository
{
    // Returns the source's cursor, creating it on first poll.
    /// <summary>
    /// The cursor for one source, across organisations, or null when there is not one yet.
    /// </summary>
    /// <remarks>
    /// Read-only, and for the polling loop alone: it decides which sources are due before it has
    /// a scope to decide it under. Creating a cursor stays on <see cref="GetOrCreateAsync"/>,
    /// which runs inside the source's own scope and therefore knows whose row it is writing.
    /// </remarks>
    Task<SourceCursor?> GetForPollingAsync(
        Guid telemetrySourceId,
        CancellationToken cancellationToken = default
    );

    Task<SourceCursor> GetOrCreateAsync(
        Guid telemetrySourceId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
