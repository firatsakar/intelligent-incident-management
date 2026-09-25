using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.EventHandlers;

/// <summary>
/// Closes the loop between an incident and the signature that opened it (Adım 24).
/// </summary>
/// <remarks>
/// <para>
/// Two things follow from an incident being closed. The signature lets go of it, so the next
/// burst of the same error opens a new incident instead of being absorbed into a closed one. And
/// the verdict is counted against the signature, which is what the history term in
/// <c>SignalScoring</c> reads the next time it fires. <c>ErrorSignature.DetachIncident</c> did both
/// from the start; until this handler nothing called it, so every signature's record stayed empty.
/// </para>
/// <para>
/// Idempotent without a ledger: the signature is found by the incident it is attached to, and
/// detaching is what breaks that link, so a redelivered event finds nothing. The same rule makes an
/// incident no signature opened — a manual one — a no-op. The price is one edge: if the signature
/// has already moved on to a newer incident when the event arrives, this verdict is not counted.
/// </para>
/// </remarks>
public sealed class IncidentResolvedEventHandler : IIntegrationEventHandler<IncidentResolvedEvent>
{
    private const string Real = "Real";
    private const string FalsePositive = "FalsePositive";

    private readonly IErrorSignatureRepository _signatures;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<IncidentResolvedEventHandler> _logger;

    public IncidentResolvedEventHandler(
        IErrorSignatureRepository signatures,
        IRealtimeNotifier realtime,
        ILogger<IncidentResolvedEventHandler> logger
    )
    {
        _signatures = signatures;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task HandleAsync(
        IncidentResolvedEvent integrationEvent,
        CancellationToken cancellationToken = default
    )
    {
        // An unknown verdict is not guessed at: counting it either way would teach the gate
        // something nobody said.
        if (integrationEvent.Verdict is not (Real or FalsePositive))
        {
            _logger.LogWarning(
                "Incident {IncidentId} was closed with an unknown verdict {Verdict}; its signature is left as it is.",
                integrationEvent.IncidentId,
                integrationEvent.Verdict
            );

            return;
        }

        // Through the query filter, in the organisation the bus established from this event.
        var signature = await _signatures.GetByCurrentIncidentAsync(
            integrationEvent.IncidentId,
            cancellationToken
        );

        if (signature is null || signature.CurrentIncidentId != integrationEvent.IncidentId)
        {
            _logger.LogDebug(
                "No signature is attached to incident {IncidentId}; nothing to release.",
                integrationEvent.IncidentId
            );

            return;
        }

        signature.DetachIncident(wasRealIncident: integrationEvent.Verdict == Real);

        await _signatures.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Released signature {SignatureId} from incident {IncidentId} as {Verdict} ({Real} real, {FalsePositive} false positive).",
            signature.Id,
            integrationEvent.IncidentId,
            integrationEvent.Verdict,
            signature.ConfirmedRealCount,
            signature.FalsePositiveCount
        );

        // After the save: what is broadcast has to be what is stored. The evidence screen's
        // signature column shows the counters this just moved.
        await _realtime.SignatureChangedAsync(ErrorSignatureDto.FromDomain(signature), cancellationToken);
    }
}
