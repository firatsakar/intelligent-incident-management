using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Repositories;

public sealed class TelemetrySourceRepository : ITelemetrySourceRepository
{
    private readonly TelemetryDbContext _context;

    public TelemetrySourceRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task<TelemetrySource?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.TelemetrySources.FirstOrDefaultAsync(
            x => x.Id == id,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<TelemetrySource>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TelemetrySources.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelemetrySource>> GetEnabledForPollingAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TelemetrySources.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.IsEnabled && x.OrganizationId != Guid.Empty)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelemetrySource>> GetEnabledAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TelemetrySources.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TelemetrySource source, CancellationToken cancellationToken = default)
    {
        await _context.TelemetrySources.AddAsync(source, cancellationToken);
    }

    public void Update(TelemetrySource source)
    {
        _context.TelemetrySources.Update(source);
    }

    public void Remove(TelemetrySource source)
    {
        _context.TelemetrySources.Remove(source);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
