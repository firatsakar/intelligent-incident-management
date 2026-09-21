using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;

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
            .LongCountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DateTime>> GetTimestampsByFingerprintAsync(
        string fingerprint,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        // One column over a BRIN-indexed range. At real log volume this wants bucketing pushed
        // into SQL, but the baseline only ever spans a handful of windows.
        return await _context
            .LogRecords.AsNoTracking()
            .Where(x => x.Fingerprint == fingerprint && x.Timestamp >= from && x.Timestamp < to)
            .Select(x => x.Timestamp)
            .ToListAsync(cancellationToken);
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
