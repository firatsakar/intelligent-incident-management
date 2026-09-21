using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Outbox;

public sealed class OutboxDispatcher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Poll loop must never die. Log and continue to the next tick.
                _logger.LogError(ex, "Outbox dispatch cycle failed. Retrying next tick.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var messages = await store.GetPendingAsync(BatchSize, cancellationToken);

        if (messages.Count == 0)
            return;

        var handlers = scope
            .ServiceProvider.GetServices<IOutboxMessageHandler>()
            .ToDictionary(handler => handler.MessageType, StringComparer.Ordinal);

        foreach (var message in messages)
        {
            try
            {
                if (!handlers.TryGetValue(message.Type, out var handler))
                    throw new NotSupportedException(
                        $"No outbox handler registered for message type '{message.Type}'."
                    );

                // Ordering is deliberate and load-bearing: the side effect happens first, and
                // only then is the row stamped. Reversing it would lose a message on a crash
                // between the two.
                await handler.HandleAsync(message, cancellationToken);

                message.ProcessedOn = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;

                _logger.LogError(
                    ex,
                    "Failed to dispatch outbox message {MessageId} (type {Type}, attempt {RetryCount}).",
                    message.Id,
                    message.Type,
                    message.RetryCount
                );
            }
        }

        await store.SaveChangesAsync(cancellationToken);
    }
}
