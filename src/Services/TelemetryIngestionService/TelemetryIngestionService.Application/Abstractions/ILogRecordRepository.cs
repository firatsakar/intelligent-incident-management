using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Abstractions;

public interface ILogRecordRepository
{
    // Which of these event ids we already hold. The cursor deliberately re-fetches its boundary
    // event every poll, so the overlap has to be filtered before insert rather than after.
    Task<IReadOnlySet<string>> GetExistingSourceEventIdsAsync(
        Guid telemetrySourceId,
        IReadOnlyCollection<string> sourceEventIds,
        CancellationToken cancellationToken = default
    );

    Task AddRangeAsync(
        IEnumerable<LogRecord> records,
        CancellationToken cancellationToken = default
    );

    // Occurrences of one fingerprint inside a time window — the count burst detection works from.
    Task<long> CountByFingerprintAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
