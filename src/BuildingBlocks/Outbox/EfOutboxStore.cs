using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Outbox;

/// <summary>
/// The outbox store every service uses, over its own DbContext.
/// </summary>
/// <remarks>
/// <para>
/// One worker per message, however many replicas run. <see cref="GetPendingAsync"/> opens
/// a transaction and claims the batch with <c>FOR UPDATE SKIP LOCKED</c>: the rows stay locked
/// while they are dispatched, and a second replica's dispatcher skips them instead of publishing
/// them again. <see cref="SaveChangesAsync"/> writes the stamps and commits, which releases them.
/// </para>
/// <para>
/// The order the dispatcher depends on is unchanged — publish, then stamp. A crash in between rolls
/// the transaction back, the locks go with the connection, and the batch is dispatched again:
/// at-least-once, as before, and never twice at once.
/// </para>
/// </remarks>
public abstract class EfOutboxStore<TContext> : IOutboxStore
    where TContext : DbContext
{
    private readonly TContext _context;

    protected EfOutboxStore(TContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default
    )
    {
        if (_context.Database.CurrentTransaction is null)
            await _context.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // Raw SQL because a row lock is not something LINQ can say. The predicate is
        // OutboxMessage.Due, spelled for Postgres.
        return await _context
            .Set<OutboxMessage>()
            .FromSql(
                $"""
                SELECT * FROM outbox_messages
                WHERE "ProcessedOn" IS NULL
                  AND "ParkedAt" IS NULL
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                ORDER BY "OccurredOn"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """
            )
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);

        if (_context.Database.CurrentTransaction is { } claim)
            await claim.CommitAsync(cancellationToken);
    }

    public Task<int> DeleteProcessedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        _context
            .Set<OutboxMessage>()
            .Where(m => m.ProcessedOn != null && m.ProcessedOn < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> CountParkedAsync(CancellationToken cancellationToken = default) =>
        _context.Set<OutboxMessage>().CountAsync(m => m.ParkedAt != null, cancellationToken);
}
