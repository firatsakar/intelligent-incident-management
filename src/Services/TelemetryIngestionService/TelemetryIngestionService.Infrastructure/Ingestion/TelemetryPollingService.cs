using MediatR;
using BuildingBlocks.SharedKernel;
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

        var enabled = await sources.GetEnabledForPollingAsync(cancellationToken);

        foreach (var source in enabled)
        {
            // A source written before organisations existed has no owner, and there is nowhere to
            // put what polling it would find. Skipped rather than polled with an empty scope,
            // which the context would refuse anyway — and said out loud, because a source that is
            // enabled and silent is the failure this platform exists to notice.
            if (source.OrganizationId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Telemetry source {SourceId} has no organisation and was not polled.",
                    source.Id
                );

                continue;
            }

            var cursor = await cursors.GetForPollingAsync(source.Id, cancellationToken);

            if (!IsDue(source, cursor))
                continue;

            // Each source gets its own scope: one source's DbContext state, and one source's
            // failure, must not touch another's.
            using var sourceScope = _scopeFactory.CreateScope();

            // The one place in the detection pipeline where an organisation is read from a row
            // rather than from a claim or a message. Nothing upstream of here has a scope to
            // inherit — this loop is woken by a timer, not by anybody — so this is where the whole
            // chain's ownership is decided, and everything after it carries what is set here.
            sourceScope
                .ServiceProvider.GetRequiredService<IOrganizationContext>()
                .Set(source.OrganizationId);

            await sourceScope
                .ServiceProvider.GetRequiredService<ISender>()
                .Send(new PollTelemetrySourceCommand(source.Id), cancellationToken);
        }
    }

    // A source with no cursor has never been polled, which makes it due. The cursor is created
    // on the first poll, inside the source's own scope.
    private static bool IsDue(TelemetrySource source, SourceCursor? cursor)
    {
        return cursor?.LastPolledAt is null
            || DateTime.UtcNow - cursor.LastPolledAt.Value
                >= TimeSpan.FromSeconds(source.PollIntervalSeconds);
    }
}
