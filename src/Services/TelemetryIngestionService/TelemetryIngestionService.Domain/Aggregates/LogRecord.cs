using BuildingBlocks.SharedKernel;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Domain.Aggregates;

// One normalised log event pulled from a source. The two timestamps are the foundation of the
// whole correlation story and are never conflated.
public sealed class LogRecord : AggregateRoot
{
    private LogRecord() { }

    public Guid TelemetrySourceId { get; private set; }

    // The source's own id for this event, so a replayed batch can be recognised.
    public string? SourceEventId { get; private set; }

    public string Service { get; private set; } = default!;
    public LogSeverity Severity { get; private set; }
    public string Message { get; private set; } = default!;

    // Message with volatile parts masked — the basis of the fingerprint.
    public string NormalizedMessage { get; private set; } = default!;

    public string? ExceptionType { get; private set; }
    public string? StackTrace { get; private set; }

    // Set for Error and Fatal records; null for everything else.
    public string? Fingerprint { get; private set; }

    // When the event happened, on the source's clock.
    public DateTime Timestamp { get; private set; }

    // When we received it. The gap between the two is ingestion lag.
    public DateTime IngestedAt { get; private set; }

    // Timestamp is implausible — meaningfully in the future. Recorded rather than rejected, so a
    // misconfigured clock is visible instead of silently distorting a window.
    public bool HasClockSkew { get; private set; }

    public static LogRecord Create(
        Guid telemetrySourceId,
        string? sourceEventId,
        string service,
        LogSeverity severity,
        string message,
        string normalizedMessage,
        string? exceptionType,
        string? stackTrace,
        string? fingerprint,
        DateTime timestamp,
        TimeSpan clockSkewTolerance
    )
    {
        var ingestedAt = DateTime.UtcNow;

        return new LogRecord
        {
            Id = Guid.NewGuid(),
            TelemetrySourceId = telemetrySourceId,
            SourceEventId = sourceEventId,
            Service = service,
            Severity = severity,
            Message = message,
            NormalizedMessage = normalizedMessage,
            ExceptionType = exceptionType,
            StackTrace = stackTrace,
            Fingerprint = fingerprint,
            Timestamp = timestamp,
            IngestedAt = ingestedAt,
            HasClockSkew = timestamp > ingestedAt.Add(clockSkewTolerance),
        };
    }
}
