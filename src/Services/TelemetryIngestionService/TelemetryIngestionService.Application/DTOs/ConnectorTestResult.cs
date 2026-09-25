namespace TelemetryIngestionService.Application.DTOs;

public sealed record ConnectorTestResult
{
    public required bool IsSuccess { get; init; }
    public string? Error { get; init; }

    // How many events the probe could see, so a source that connects but matches nothing is
    // distinguishable from one that connects and works.
    public int? MatchedEvents { get; init; }

    // For a pushed source, which cannot be probed from here: when data last arrived.
    public DateTime? LastReceivedAt { get; init; }

    public static ConnectorTestResult Success(int matchedEvents) =>
        new() { IsSuccess = true, MatchedEvents = matchedEvents };

    public static ConnectorTestResult Failure(string error) =>
        new() { IsSuccess = false, Error = error };
}
