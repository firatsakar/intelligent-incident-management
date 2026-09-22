namespace TelemetryIngestionService.Application.DTOs;

/// <summary>
/// What one poll of one source brought in.
///
/// This is the deliberate alternative to pushing log records. A poll writes up to two hundred rows
/// per source, so a row-level push would be roughly forty messages a second at the configured
/// floor — fanned to every connected client, because the hubs have no groups. The screen does not
/// need the rows; it needs to know that its window is out of date and by how much, which is one
/// message and one number.
/// </summary>
public sealed record IngestionTickDto
{
    public required Guid TelemetrySourceId { get; init; }

    /// <summary>New records actually written, after the cursor overlap was filtered out.</summary>
    public required int NewRecords { get; init; }

    /// <summary>Distinct fingerprints touched, which is what the detector then looked at.</summary>
    public required int TouchedSignatures { get; init; }

    /// <summary>
    /// The newest event timestamp in the batch, so a client can tell whether the arrival falls
    /// inside the window it is showing rather than assuming it does.
    /// </summary>
    public required DateTime? LatestEventAt { get; init; }

    public required DateTime CompletedAt { get; init; }
}
