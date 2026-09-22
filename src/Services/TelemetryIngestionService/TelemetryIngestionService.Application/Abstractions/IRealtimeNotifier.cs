using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Abstractions;

// Every signal, not just the promoted ones. The screen this feeds shows what the gate decided
// *not* to wake anyone for alongside what it did, and the heat map is built by aggregating the
// same rows client-side — so a pushed signal updates the map without a request.
//
// That aggregation is only possible because SignalDto carries the signature's service and error
// (Parça 1); the signal on its own knows nothing but an id.
public interface IRealtimeNotifier
{
    Task SignalRecordedAsync(SignalDto signal, CancellationToken cancellationToken = default);

    // The signature changes in the same transaction as the signal — it gains an incident, its
    // counters move — and none of that used to be broadcast, so the evidence screen's signature
    // column went stale while signals were still arriving live. One message per touched
    // signature, which is a single-digit number per poll cycle.
    Task SignatureChangedAsync(
        ErrorSignatureDto signature,
        CancellationToken cancellationToken = default
    );

    // Configuration, for the second operator or the second tab.
    Task SourceChangedAsync(
        TelemetrySourceDto source,
        CancellationToken cancellationToken = default
    );

    Task SourceDeletedAsync(Guid sourceId, CancellationToken cancellationToken = default);

    // One message per poll cycle carrying how much arrived, rather than one per row. A poll can
    // write two hundred log records per source, and at the five-second floor that is roughly
    // forty messages a second fanned to every connected client — the evidence screen would be
    // paying far more to hear about rows than it would to re-read them. This lets it say "N new
    // records in this window" and leave the decision to the operator.
    Task IngestionCompletedAsync(
        IngestionTickDto tick,
        CancellationToken cancellationToken = default
    );
}
