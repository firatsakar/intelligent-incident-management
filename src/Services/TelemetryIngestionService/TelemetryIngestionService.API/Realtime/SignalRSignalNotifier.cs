using BuildingBlocks.Web;
using BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.SignalR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.API.Realtime;

public sealed class SignalRSignalNotifier : IRealtimeNotifier
{
    private readonly IHubContext<SignalHub> _hub;
    private readonly ILogger<SignalRSignalNotifier> _logger;
    private readonly IOrganizationContext _organization;

    public SignalRSignalNotifier(
        IHubContext<SignalHub> hub,
        ILogger<SignalRSignalNotifier> logger,
        IOrganizationContext organization
    )
    {
        _hub = hub;
        _logger = logger;
        _organization = organization;
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
    ) => SendAsync("sourceChanged", source, source.Id, cancellationToken, adminsOnly: true);

    public Task SourceDeletedAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
        SendAsync("sourceDeleted", sourceId, sourceId, cancellationToken, adminsOnly: true);

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
        CancellationToken cancellationToken,
        bool adminsOnly = false
    )
    {
        try
        {
            await Audience(adminsOnly).SendAsync(message, payload, cancellationToken);
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

    // The organisation in scope is the organisation whose row was just written: the same scope
    // stamped it and the same query filter would read it back. With no scope there is no audience
    // at all — never Clients.All, which is what every one of these used to be and is exactly the
    // leak this exists to close. The send then goes nowhere, and the caller swallows failures
    // anyway because a broadcast is the least important thing a command does.
    //
    // Configuration changes go to the organisation's Admins only: only they may read it at all.
    private IClientProxy Audience(bool adminsOnly = false)
    {
        var organizationId = _organization.OrganizationId;

        if (organizationId is null)
        {
            _logger.LogWarning("Realtime push skipped: no organisation in scope to address it to.");

            return NoAudience.Instance;
        }

        return _hub.Clients.Group(
            adminsOnly
                ? OrganizationGroups.AdminsOf(organizationId.Value)
                : OrganizationGroups.For(organizationId.Value)
        );
    }

    /// <summary>A proxy that sends to nobody, for the case where there is nobody it may send to.</summary>
    private sealed class NoAudience : IClientProxy
    {
        public static readonly NoAudience Instance = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
