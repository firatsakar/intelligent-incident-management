using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Infrastructure.Persistence.Repositories;

public sealed class SignalRepository : ISignalRepository
{
    private readonly TelemetryDbContext _context;

    public SignalRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task<Signal?> GetLatestForSignatureAsync(
        Guid errorSignatureId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Signals.Where(x => x.ErrorSignatureId == errorSignatureId)
            .OrderByDescending(x => x.WindowEnd)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Signal>> GetByStatusAsync(
        SignalStatus status,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Signals.AsNoTracking()
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.DetectedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Signal> Items, int TotalCount)> GetRecentAsync(
        DateTime from,
        DateTime to,
        int limit,
        int offset,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context
            .Signals.AsNoTracking()
            .Where(x => x.DetectedAt >= from && x.DetectedAt <= to);

        var total = await query.CountAsync(cancellationToken);

        // Newest first, which is the opposite of what this method used to return. The caller
        // shows a truncated list, and truncating a window means dropping its far end — so the end
        // that gets dropped has to be the old one.
        var items = await query
            .OrderByDescending(x => x.DetectedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<SignalStatsRow>> GetWindowStatsRowsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        // Five scalars per signal and nothing else: no score breakdown dictionary, no reason
        // string. Those are what make a Signal expensive and neither is counted here.
        return await _context
            .Signals.AsNoTracking()
            .Where(x => x.DetectedAt >= from && x.DetectedAt <= to)
            .Select(x => new SignalStatsRow(
                x.ErrorSignatureId,
                x.Status,
                x.DetectedAt,
                x.IncidentId,
                x.OccurrenceCount
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        await _context.Signals.AddAsync(signal, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
