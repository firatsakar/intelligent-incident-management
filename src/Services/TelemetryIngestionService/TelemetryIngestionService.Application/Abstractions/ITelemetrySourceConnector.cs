using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Abstractions;

// One implementation per external system, resolved by kind through keyed DI — the same shape as
// NotificationService's INotificationChannel. A connector holds no per-customer state; everything
// it needs comes from the source's config.
public interface ITelemetrySourceConnector
{
    TelemetrySourceKind Kind { get; }

    // Reads the next batch after the cursor. Returning fewer than maxEvents means the source is
    // caught up.
    Task<ConnectorFetchResult> FetchAsync(
        TelemetrySource source,
        SourceCursor cursor,
        int maxEvents,
        CancellationToken cancellationToken = default
    );

    // Proves the settings before an incident depends on them.
    Task<ConnectorTestResult> TestAsync(
        TelemetrySource source,
        CancellationToken cancellationToken = default
    );
}
