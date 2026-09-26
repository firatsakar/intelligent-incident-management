using AgentOrchestrator.Infrastructure.Persistence;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AgentOrchestrator.Infrastructure.Outbox;

public sealed class AgentOutboxStore : IOutboxStore
{
    private readonly AgentDbContext _context;

    public AgentOutboxStore(AgentDbContext context)
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
