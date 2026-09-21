using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Repositories;

public sealed class DetectionRuleRepository : IDetectionRuleRepository
{
    private readonly TelemetryDbContext _context;

    public DetectionRuleRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DetectionRule>> GetEnabledAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .DetectionRules.AsNoTracking()
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DetectionRules.CountAsync(cancellationToken);
    }

    public async Task AddAsync(DetectionRule rule, CancellationToken cancellationToken = default)
    {
        await _context.DetectionRules.AddAsync(rule, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
