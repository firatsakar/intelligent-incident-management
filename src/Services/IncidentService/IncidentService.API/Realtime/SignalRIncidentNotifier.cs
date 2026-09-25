using BuildingBlocks.Web;
using BuildingBlocks.SharedKernel;
using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace IncidentService.API.Realtime;

public sealed class SignalRIncidentNotifier : IRealtimeNotifier
{
    private readonly IHubContext<IncidentHub> _hub;
    private readonly ILogger<SignalRIncidentNotifier> _logger;
    private readonly IOrganizationContext _organization;

    public SignalRIncidentNotifier(
        IHubContext<IncidentHub> hub,
        ILogger<SignalRIncidentNotifier> logger,
        IOrganizationContext organization
    )
    {
        _hub = hub;
        _logger = logger;
        _organization = organization;
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
            await Audience().SendAsync(method, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast {Method} over the incident hub.", method);
        }
    }

    // The organisation in scope is the organisation whose row was just written: the same scope
    // stamped it and the same query filter would read it back. With no scope there is no audience
    // at all — never Clients.All, which is what every one of these used to be and is exactly the
    // leak this exists to close. The send then goes nowhere, and the caller swallows failures
    // anyway because a broadcast is the least important thing a command does.
    private IClientProxy Audience()
    {
        var organizationId = _organization.OrganizationId;

        if (organizationId is null)
        {
            _logger.LogWarning("Realtime push skipped: no organisation in scope to address it to.");

            return NoAudience.Instance;
        }

        return _hub.Clients.Group(OrganizationGroups.For(organizationId.Value));
    }

    /// <summary>A proxy that sends to nobody, for the case where there is nobody it may send to.</summary>
    private sealed class NoAudience : IClientProxy
    {
        public static readonly NoAudience Instance = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
