using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.DTOs;

// What a connector hands back: one log event from the external system, already mapped onto our
// vocabulary but not yet normalised or fingerprinted.
public sealed record RawLogEvent
{
    public string? SourceEventId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required LogSeverity Severity { get; init; }
    public required string Service { get; init; }
    public required string Message { get; init; }

    // The message template, when the source keeps one — "Checkout failed for order {OrderId}".
    // A template is a natural normalisation: the volatile parts are already named placeholders,
    // so fingerprinting can use it directly instead of guessing with regexes. Null for sources
    // that only store rendered text.
    public string? MessageTemplate { get; init; }

    public string? ExceptionType { get; init; }
    public string? StackTrace { get; init; }
}
