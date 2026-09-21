using Microsoft.AspNetCore.SignalR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.API.Realtime;

public sealed class SignalRSignalNotifier : IRealtimeNotifier
{
    private readonly IHubContext<SignalHub> _hub;
    private readonly ILogger<SignalRSignalNotifier> _logger;

    public SignalRSignalNotifier(
        IHubContext<SignalHub> hub,
        ILogger<SignalRSignalNotifier> logger
    )
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task SignalRecordedAsync(
        SignalDto signal,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await _hub.Clients.All.SendAsync("signalRecorded", signal, cancellationToken);
        }
        catch (Exception ex)
        {
            // Detection has already committed, along with any promotion event the interceptor
            // harvested into the outbox. Losing the broadcast costs a stale screen until the next
            // read; letting it throw would cost the poll cycle.
            _logger.LogWarning(
                ex,
                "Failed to broadcast signal {SignalId} over the signal hub.",
                signal.Id
            );
        }
    }
}
