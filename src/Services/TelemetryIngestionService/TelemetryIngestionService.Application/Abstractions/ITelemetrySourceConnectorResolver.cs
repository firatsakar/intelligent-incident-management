using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Abstractions;

// Keeps keyed-DI resolution out of the Application layer.
public interface ITelemetrySourceConnectorResolver
{
    ITelemetrySourceConnector Resolve(TelemetrySourceKind kind);
}
