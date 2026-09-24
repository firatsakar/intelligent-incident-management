namespace TelemetryIngestionService.Application.DTOs;

public sealed record IngestResult
{
    public required int Received { get; init; }

    // Dropped because we already held them. Expected on every poll — the cursor re-fetches its
    // boundary event on purpose — and on a pushed batch the sender retried.
    public required int Duplicates { get; init; }

    // Rows written.
    public required int Stored { get; init; }

    // Events counted onto a stored sample instead of getting a row of their own. Stored plus
    // Folded is every new event in the batch.
    public required int Folded { get; init; }

    public required int SignaturesTouched { get; init; }

    public static IngestResult Nothing(int received = 0, int duplicates = 0) =>
        new()
        {
            Received = received,
            Duplicates = duplicates,
            Stored = 0,
            Folded = 0,
            SignaturesTouched = 0,
        };
}
