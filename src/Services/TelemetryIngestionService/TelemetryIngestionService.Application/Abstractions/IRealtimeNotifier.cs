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
}
