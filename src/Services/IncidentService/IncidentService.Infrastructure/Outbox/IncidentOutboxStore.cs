using BuildingBlocks.Outbox;
using IncidentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IncidentService.Infrastructure.Outbox;

// The same store the other three services have. Not a shared building block: each one is a thin
// window onto its own DbContext, and the context is the one thing they cannot share.
public sealed class IncidentOutboxStore : IOutboxStore
{
    private readonly IncidentDbContext _context;

    public IncidentOutboxStore(IncidentDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .OutboxMessages.Where(OutboxMessage.Due(DateTimeOffset.UtcNow))
            .OrderBy(m => m.OccurredOn)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .OutboxMessages.Where(m => m.ProcessedOn != null && m.ProcessedOn < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> CountParkedAsync(CancellationToken cancellationToken = default) =>
        _context.OutboxMessages.CountAsync(m => m.ParkedAt != null, cancellationToken);
}
