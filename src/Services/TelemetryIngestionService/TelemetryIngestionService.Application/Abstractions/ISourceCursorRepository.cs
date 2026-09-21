using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Abstractions;

public interface ISourceCursorRepository
{
    // Returns the source's cursor, creating it on first poll.
    Task<SourceCursor> GetOrCreateAsync(
        Guid telemetrySourceId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
