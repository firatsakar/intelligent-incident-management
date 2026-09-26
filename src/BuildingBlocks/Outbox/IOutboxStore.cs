namespace BuildingBlocks.Outbox;

// The one thing the dispatcher cannot know: which database the rows live in. Each service
// implements this over its own DbContext.
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // Processed rows are history nobody reads. Left alone they grow without limit and slow the
    // pending query down.
    Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default
    );

    // Rows that gave up (Adım 28): what the hourly sweep reports while any remain.
    Task<int> CountParkedAsync(CancellationToken cancellationToken = default);
}
