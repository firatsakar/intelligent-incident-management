namespace TelemetryIngestionService.Application.DTOs;

public sealed record PollResult
{
    public required int Fetched { get; init; }

    // Dropped because we already held them — expected on every poll, since the cursor re-fetches
    // its boundary event on purpose.
    public required int Duplicates { get; init; }

    public required int Stored { get; init; }

    // Counted onto a stored sample rather than given a row; see SampleFolding.
    public int Folded { get; init; }

    public required int SignaturesTouched { get; init; }
    public string? Error { get; init; }

    public static PollResult Failed(string error) =>
        new()
        {
            Fetched = 0,
            Duplicates = 0,
            Stored = 0,
            SignaturesTouched = 0,
            Error = error,
        };
}
