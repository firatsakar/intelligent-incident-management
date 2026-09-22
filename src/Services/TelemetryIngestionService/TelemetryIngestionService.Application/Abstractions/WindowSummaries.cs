using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Abstractions;

/// <summary>
/// Counts only — the log side of the funnel, aggregated in the database rather than pulled back.
/// This one really is grouped in SQL: the buckets are categorical (service, severity), so there
/// is no date arithmetic and therefore none of the session-time-zone ambiguity that keeps the
/// incident day buckets in memory.
/// </summary>
public sealed record LogWindowSummary(
    int TotalRecords,
    int DistinctFingerprints,
    IReadOnlyDictionary<string, int> ByService,
    IReadOnlyDictionary<LogSeverity, int> BySeverity
);

/// <summary>
/// One signal reduced to what the funnel and the per-service rollup count. The signature is
/// carried by id; the handler resolves the service and the message in a single batch read, the
/// same way <c>GetSignalsQueryHandler</c> already does.
/// </summary>
public sealed record SignalStatsRow(
    Guid ErrorSignatureId,
    SignalStatus Status,
    DateTime DetectedAt,
    Guid? IncidentId,
    // long, matching Signal.OccurrenceCount: a single storm already reaches the hundreds, and
    // this one gets summed across a whole window.
    long OccurrenceCount
);
