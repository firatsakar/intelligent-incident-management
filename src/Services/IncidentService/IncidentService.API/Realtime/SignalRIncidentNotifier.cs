using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace IncidentService.API.Realtime;

public sealed class SignalRIncidentNotifier : IRealtimeNotifier
{
    private readonly IHubContext<IncidentHub> _hub;
    private readonly ILogger<SignalRIncidentNotifier> _logger;

    public SignalRIncidentNotifier(
        IHubContext<IncidentHub> hub,
        ILogger<SignalRIncidentNotifier> logger
    )
    {
        _hub = hub;
        _logger = logger;
    }

    public Task IncidentCreatedAsync(
        IncidentDto incident,
        CancellationToken cancellationToken = default
    ) => SendAsync("incidentCreated", incident, cancellationToken);

    public Task IncidentChangedAsync(
        IncidentDto incident,
        CancellationToken cancellationToken = default
    ) => SendAsync("incidentChanged", incident, cancellationToken);

    // A broadcast is the last thing a command does and the least important. The incident is
    // already committed by this point, so a failed push must not turn a successful write into a
    // 500, nor dead-letter the message that caused it. Whoever is watching gets it on their next
    // read instead.
    private async Task SendAsync(string method, object payload, CancellationToken cancellationToken)
    {
        try
        {
            await _hub.Clients.All.SendAsync(method, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast {Method} over the incident hub.", method);
        }
    }
}
