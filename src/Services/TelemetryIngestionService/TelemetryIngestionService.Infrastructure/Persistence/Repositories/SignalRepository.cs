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

    public async Task<IReadOnlyList<Signal>> GetRecentAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Signals.AsNoTracking()
            .Where(x => x.DetectedAt >= from && x.DetectedAt <= to)
            .OrderBy(x => x.DetectedAt)
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
