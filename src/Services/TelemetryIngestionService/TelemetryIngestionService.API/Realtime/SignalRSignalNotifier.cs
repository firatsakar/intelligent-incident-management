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

    public Task SignalRecordedAsync(
        SignalDto signal,
        CancellationToken cancellationToken = default
    ) => SendAsync("signalRecorded", signal, signal.Id, cancellationToken);

    public Task SignatureChangedAsync(
        ErrorSignatureDto signature,
        CancellationToken cancellationToken = default
    ) => SendAsync("signatureChanged", signature, signature.Id, cancellationToken);

    public Task SourceChangedAsync(
        TelemetrySourceDto source,
        CancellationToken cancellationToken = default
    ) => SendAsync("sourceChanged", source, source.Id, cancellationToken);

    public Task SourceDeletedAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
        SendAsync("sourceDeleted", sourceId, sourceId, cancellationToken);

    public Task IngestionCompletedAsync(
        IngestionTickDto tick,
        CancellationToken cancellationToken = default
    ) => SendAsync("ingestionCompleted", tick, tick.TelemetrySourceId, cancellationToken);

    /// <summary>
    /// Every broadcast in this service is best-effort. Detection has already committed by the
    /// time any of these run, along with any promotion event the interceptor harvested into the
    /// outbox; losing a broadcast costs a stale screen until the next read, while letting one
    /// throw would cost the poll cycle.
    /// </summary>
    private async Task SendAsync(
        string message,
        object payload,
        Guid id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _hub.Clients.All.SendAsync(message, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to broadcast {Message} for {EntityId} over the signal hub.",
                message,
                id
            );
        }
    }
}
