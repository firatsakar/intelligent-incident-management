using BuildingBlocks.SharedKernel;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Domain.Aggregates;

// One normalised log event pulled from a source. The two timestamps are the foundation of the
// whole correlation story and are never conflated.
public sealed class LogRecord : AggregateRoot
{
    private LogRecord() { }

    /// <summary>
    /// Whose row this is. Inherited from the telemetry source the pipeline started at, carried
    /// here explicitly because nothing in this model has a navigation to inherit through.
    /// </summary>
    public Guid OrganizationId { get; private set; }

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

    /// <summary>
    /// How many events this row stands for. One, unless it is the sample a burst was folded onto.
    /// </summary>
    /// <remarks>
    /// Not every line of a storm is worth a row: three hundred copies of one error differ only in
    /// the ids the fingerprint already masks. A batch keeps a few of each signature as samples and
    /// counts the rest onto the newest one (<see cref="Services.SampleFolding"/>). Everything that
    /// asks "how many" — burst detection, the baseline, the funnel — sums this column rather than
    /// counting rows, so a folded storm weighs exactly what the unfolded one did.
    /// </remarks>
    public int Occurrences { get; private set; } = 1;

    // When the event happened, on the source's clock.
    public DateTime Timestamp { get; private set; }

    // When we received it. The gap between the two is ingestion lag.
    public DateTime IngestedAt { get; private set; }

    // Timestamp is implausible — meaningfully in the future. Recorded rather than rejected, so a
    // misconfigured clock is visible instead of silently distorting a window.
    public bool HasClockSkew { get; private set; }

    public static LogRecord Create(
        Guid organizationId,
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
        TimeSpan clockSkewTolerance,
        int occurrences = 1
    )
    {
        if (occurrences < 1)
            throw new ArgumentOutOfRangeException(
                nameof(occurrences),
                occurrences,
                "A stored record stands for at least the one event it is."
            );

        var ingestedAt = DateTime.UtcNow;

        return new LogRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            TelemetrySourceId = telemetrySourceId,
            SourceEventId = sourceEventId,
            Service = service,
            Severity = severity,
            Message = message,
            NormalizedMessage = normalizedMessage,
            ExceptionType = exceptionType,
            StackTrace = stackTrace,
            Fingerprint = fingerprint,
            Occurrences = occurrences,
            Timestamp = timestamp,
            IngestedAt = ingestedAt,
            HasClockSkew = timestamp > ingestedAt.Add(clockSkewTolerance),
        };
    }
}
