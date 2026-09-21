namespace TelemetryIngestionService.Application.DTOs;

public sealed record ConnectorFetchResult
{
    public required IReadOnlyList<RawLogEvent> Events { get; init; }

    // Where the next poll should resume from. Opaque to everything except the connector that
    // produced it.
    public string? NextPosition { get; init; }

    public DateTime? LastEventTimestamp { get; init; }

    public static ConnectorFetchResult Empty(string? position = null) =>
        new() { Events = [], NextPosition = position };
}
