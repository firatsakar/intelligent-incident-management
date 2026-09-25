namespace TelemetryIngestionService.Application.DTOs;

/// <summary>
/// The detection gate's own story, as numbers.
///
/// Every stage narrows: many log records fold into far fewer signatures, only some of those cross
/// a rule and become signals, and only some of those clear the promotion threshold. The stage
/// that matters most is the one in the middle — <b>what the gate looked at and deliberately did
/// not raise</b>. A system that only reported what it escalated would be indistinguishable from
/// an alerting rule, which is the claim this screen exists to test.
///
/// Days and windows are UTC throughout, matching the incident statistics.
/// </summary>
public sealed record TelemetryStatsDto
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }

    public required FunnelDto Funnel { get; init; }

    /// <summary>One row per service that produced anything in the window.</summary>
    public required IReadOnlyList<ServiceHealthDto> Services { get; init; }
}

public sealed record FunnelDto
{
    /// <summary>Error and fatal records ingested in the window.</summary>
    public required int LogRecords { get; init; }

    /// <summary>
    /// Distinct fingerprints behind those records. The drop from the line above is the whole
    /// point of fingerprinting: a storm of two hundred identical failures is one problem.
    /// </summary>
    public required int Signatures { get; init; }

    /// <summary>Signatures that crossed a detection rule and produced a signal.</summary>
    public required int Signals { get; init; }

    /// <summary>Signals by band — Promoted, Weak, Recorded, Deduplicated, Suppressed.</summary>
    public required IReadOnlyDictionary<string, int> SignalsByStatus { get; init; }

    /// <summary>
    /// Signals the gate saw and chose not to wake anyone for. Reported on its own rather than
    /// left to be derived, because it is the number the product is arguing about.
    /// </summary>
    public required int NotRaised { get; init; }

    public required IReadOnlyDictionary<string, int> LogRecordsBySeverity { get; init; }
}

public sealed record ServiceHealthDto
{
    public required string Service { get; init; }
    public required int LogRecords { get; init; }
    public required int Signals { get; init; }
    public required int Promoted { get; init; }

    /// <summary>Distinct incidents these signals were attached to.</summary>
    public required int Incidents { get; init; }

    /// <summary>The signature responsible for the most occurrences here, if there is one.</summary>
    public required string? TopSignature { get; init; }
    public required long TopSignatureOccurrences { get; init; }

    /// <summary>Most recent signal detection, not most recent log line.</summary>
    public required DateTime? LastSignalAt { get; init; }
}
