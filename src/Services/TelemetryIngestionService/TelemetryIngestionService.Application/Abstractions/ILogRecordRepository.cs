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

    // Just the timestamps, for bucketing into the rate baseline. One column, and the range is
    // BRIN-indexed.
    Task<IReadOnlyList<DateTime>> GetTimestampsByFingerprintAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    // How many distinct services this signature has been seen in — the blast-radius input.
    Task<int> CountDistinctServicesAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    // A crash is not a judgement call, so it bypasses scoring entirely.
    Task<bool> HasFatalAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    // One stack trace to stand for the burst, for the evidence summary.
    Task<string?> GetSampleStackTraceAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    // Everything in a window, capped — what the evidence endpoint shows a human.
    Task<(IReadOnlyList<LogRecord> Records, int TotalCount)> GetWindowAsync(
        string? service,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken cancellationToken = default
    );

    // The log side of the funnel: totals and categorical splits, counted in the database. No rows
    // come back, so the width of the window does not decide the cost of the read.
    Task<LogWindowSummary> GetWindowSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
