using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Outbox;

// Dispatched rows are kept for a while so a delivery can be traced after the fact, then dropped.
// Without this the table only ever grows, and the pending query pays for all of it.
public sealed class OutboxCleanupService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxCleanupService> _logger;

    public OutboxCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxCleanupService> logger
    )
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
                using var scope = _scopeFactory.CreateScope();

                var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

                var removed = await store.DeleteProcessedBeforeAsync(
                    DateTimeOffset.UtcNow - Retention,
                    stoppingToken
                );

                if (removed > 0)
                    _logger.LogInformation("Removed {Count} processed outbox row(s).", removed);

                // Said every hour for as long as it is true — a parked row is an event some other
                // service never received, and one log line at the moment it parked is easy to miss.
                var parked = await store.CountParkedAsync(stoppingToken);

                if (parked > 0)
                {
                    _logger.LogWarning(
                        "{Count} outbox row(s) are parked after {Attempts} failed attempts and will not be dispatched again until requeued (clear ParkedAt).",
                        parked,
                        OutboxMessage.MaxAttempts
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox cleanup failed. Retrying next sweep.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
