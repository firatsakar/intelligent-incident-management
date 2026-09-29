using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TelemetryIngestionService.Infrastructure.Persistence;

/// <summary>
/// Drops log records older than the retention period. Without it <c>log_records</c> only
/// ever grows: folding keeps a burst to a few rows, but a steady error rate is still a row a poll.
/// </summary>
/// <remarks>
/// <para>
/// By the source's timestamp, which the table has a BRIN index on. Nothing references a log record:
/// signatures keep their own counters and signals their own evidence summary, so what an incident
/// says about its logs outlives the logs.
/// </para>
/// <para>
/// In batches, so a first sweep over months of rows does not hold one long transaction. Every
/// organisation's rows, like the outbox dispatcher — retention is the installation's, not a tenant's.
/// </para>
/// </remarks>
public sealed class LogRetentionService : BackgroundService
{
    public const string RetentionDaysKey = "Telemetry:LogRetentionDays";

    public const int DefaultRetentionDays = 7;

    private const int BatchSize = 5_000;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogRetentionService> _logger;
    private readonly TimeSpan _retention;

    public LogRetentionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<LogRetentionService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // At least a day: the busiest screens read up to seven, and a baseline reads twelve windows
        // back. A value that deleted the logs a detection was about to read would be a silent bug.
        var days = configuration.GetValue<int?>(RetentionDaysKey) ?? DefaultRetentionDays;
        _retention = TimeSpan.FromDays(Math.Max(days, 1));
    }

    public TimeSpan Retention => _retention;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = await SweepAsync(DateTime.UtcNow - _retention, stoppingToken);

                if (removed > 0)
                    _logger.LogInformation(
                        "Removed {Count} log record(s) older than {Days} day(s).",
                        removed,
                        _retention.TotalDays
                    );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Log retention sweep failed. Retrying next sweep.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }

    /// <summary>Deletes every log record stamped before <paramref name="cutoff"/>; returns how many.</summary>
    public async Task<int> SweepAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        var total = 0;

        while (true)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            var removed = await context
                .LogRecords.IgnoreQueryFilters()
                .Where(x => x.Timestamp < cutoff)
                .OrderBy(x => x.Timestamp)
                .Take(BatchSize)
                .ExecuteDeleteAsync(cancellationToken);

            total += removed;

            if (removed < BatchSize)
                return total;
        }
    }
}
