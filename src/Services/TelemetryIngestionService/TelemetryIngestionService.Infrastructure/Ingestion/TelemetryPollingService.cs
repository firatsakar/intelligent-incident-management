using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.PollTelemetrySource;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Ingestion;

// Drives the ingestion loop. It only decides *when* a source is due; everything about fetching
// and storing lives in the command handler.
public sealed class TelemetryPollingService : BackgroundService
{
    // The scheduler ticks faster than any source's interval so a source with a short interval is
    // not held back by the loop itself.
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryPollingService> _logger;

    public TelemetryPollingService(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryPollingService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry polling started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollDueSourcesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // The loop must never die. A failure here is the scheduler itself, not a source —
                // per-source failures are handled and recorded inside the command.
                _logger.LogError(ex, "Telemetry polling cycle failed. Retrying next tick.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    private async Task PollDueSourcesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var sources = scope.ServiceProvider.GetRequiredService<ITelemetrySourceRepository>();
        var cursors = scope.ServiceProvider.GetRequiredService<ISourceCursorRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var enabled = await sources.GetEnabledAsync(cancellationToken);

        foreach (var source in enabled)
        {
            var cursor = await cursors.GetOrCreateAsync(source.Id, cancellationToken);

            if (!IsDue(source, cursor))
                continue;

            // Each source gets its own scope: one source's DbContext state, and one source's
            // failure, must not touch another's.
            using var sourceScope = _scopeFactory.CreateScope();

            await sourceScope
                .ServiceProvider.GetRequiredService<ISender>()
                .Send(new PollTelemetrySourceCommand(source.Id), cancellationToken);
        }
    }

    private static bool IsDue(TelemetrySource source, SourceCursor cursor)
    {
        return cursor.LastPolledAt is null
            || DateTime.UtcNow - cursor.LastPolledAt.Value
                >= TimeSpan.FromSeconds(source.PollIntervalSeconds);
    }
}
