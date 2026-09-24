using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Infrastructure.Persistence.Repositories;

public sealed class LogRecordRepository : ILogRecordRepository
{
    private readonly TelemetryDbContext _context;

    public LogRecordRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlySet<string>> GetExistingSourceEventIdsAsync(
        Guid telemetrySourceId,
        IReadOnlyCollection<string> sourceEventIds,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceEventIds.Count == 0)
            return new HashSet<string>();

        var known = await _context
            .LogRecords.AsNoTracking()
            .Where(x =>
                x.TelemetrySourceId == telemetrySourceId
                && x.SourceEventId != null
                && sourceEventIds.Contains(x.SourceEventId)
            )
            .Select(x => x.SourceEventId!)
            .ToListAsync(cancellationToken);

        return known.ToHashSet();
    }

    public async Task AddRangeAsync(
        IEnumerable<LogRecord> records,
        CancellationToken cancellationToken = default
    )
    {
        await _context.LogRecords.AddRangeAsync(records, cancellationToken);
    }

    public async Task<long> CountByFingerprintAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .LogRecords.AsNoTracking()
            .Where(x => x.Fingerprint == fingerprint && x.Timestamp >= from && x.Timestamp <= to)
            .SumAsync(x => (long)x.Occurrences, cancellationToken);
    }

    public async Task<IReadOnlyList<WeightedTimestamp>> GetOccurrencesByFingerprintAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        // Two columns over a BRIN-indexed range. At real log volume this wants bucketing pushed
        // into SQL, but the baseline only ever spans a handful of windows — and folding keeps the
        // row count per window bounded however loud the signature was.
        var rows = await _context
            .LogRecords.AsNoTracking()
            .Where(x => x.Fingerprint == fingerprint && x.Timestamp >= from && x.Timestamp < to)
            .Select(x => new { x.Timestamp, x.Occurrences })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new WeightedTimestamp(x.Timestamp, x.Occurrences)).ToList();
    }

    public async Task<int> CountDistinctServicesAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .LogRecords.AsNoTracking()
            .Where(x => x.Fingerprint == fingerprint && x.Timestamp >= from && x.Timestamp <= to)
            .Select(x => x.Service)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<bool> HasFatalAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .LogRecords.AsNoTracking()
            .AnyAsync(
                x =>
                    x.Fingerprint == fingerprint
                    && x.Timestamp >= from
                    && x.Timestamp <= to
                    && x.Severity == LogSeverity.Fatal,
                cancellationToken
            );
    }

    public async Task<string?> GetSampleStackTraceAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .LogRecords.AsNoTracking()
            .Where(x =>
                x.Fingerprint == fingerprint
                && x.Timestamp >= from
                && x.Timestamp <= to
                && x.StackTrace != null
            )
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.StackTrace)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<LogRecord> Records, int TotalCount)> GetWindowAsync(
        string? service,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.LogRecords.AsNoTracking().Where(x => x.Timestamp >= from && x.Timestamp <= to);

        if (!string.IsNullOrWhiteSpace(service))
            query = query.Where(x => x.Service == service);

        var total = await query.CountAsync(cancellationToken);

        // The count is reported in full even though the rows are capped, so a truncated view is
        // obviously truncated rather than quietly misleading.
        var records = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (records, total);
    }

    public async Task<LogWindowSummary> GetWindowSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.LogRecords.AsNoTracking()
            .Where(x => x.Timestamp >= from && x.Timestamp <= to);

        // Three aggregates, three round trips, and no rows returned by any of them. The
        // alternative — one pass in memory — would make the cost of drawing a funnel depend on
        // how bad the customer's week was, which is precisely backwards.
        var total = await query.SumAsync(x => x.Occurrences, cancellationToken);

        var distinctFingerprints = await query
            .Select(x => x.Fingerprint)
            .Distinct()
            .CountAsync(cancellationToken);

        var byService = await query
            .GroupBy(x => x.Service)
            .Select(g => new { Service = g.Key, Count = g.Sum(x => x.Occurrences) })
            .ToListAsync(cancellationToken);

        var bySeverity = await query
            .GroupBy(x => x.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Sum(x => x.Occurrences) })
            .ToListAsync(cancellationToken);

        return new LogWindowSummary(
            total,
            distinctFingerprints,
            byService.ToDictionary(x => x.Service, x => x.Count),
            bySeverity.ToDictionary(x => x.Severity, x => x.Count)
        );
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
