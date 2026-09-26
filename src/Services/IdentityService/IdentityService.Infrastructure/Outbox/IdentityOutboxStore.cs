using BuildingBlocks.Outbox;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Outbox;

public sealed class IdentityOutboxStore : IOutboxStore
{
    private readonly IdentityDbContext _context;

    public IdentityOutboxStore(IdentityDbContext context)
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
