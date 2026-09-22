namespace IncidentService.Application.DTOs;

/// <summary>
/// What the dashboard needs in one read: the shape of incident arrivals over time, the
/// distribution of what is open right now, and how much of it the platform noticed on its own.
///
/// Days are bucketed in UTC. Bucketing in the operator's zone would mean the client sending an
/// offset and the server trusting it; over a 90-day chart a handful of incidents landing in an
/// adjacent bucket at the day boundary is not a visible difference. The screen says which it is
/// rather than leaving the reader to guess.
/// </summary>
public sealed record IncidentStatsDto
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }

    /// <summary>One entry per UTC day in the window, including days with nothing on them.</summary>
    public required IReadOnlyList<IncidentDayBucketDto> Days { get; init; }

    public required IReadOnlyDictionary<string, int> ByPriority { get; init; }
    public required IReadOnlyDictionary<string, int> ByStatus { get; init; }
    public required IReadOnlyDictionary<string, int> BySource { get; init; }

    /// <summary>
    /// Everything currently unresolved, <b>ignoring the window</b>. An incident opened six weeks
    /// ago and still open is exactly the thing an operator needs to see, and a window filter
    /// would hide it.
    /// </summary>
    public required IReadOnlyDictionary<string, int> OpenByPriority { get; init; }

    public required int Total { get; init; }
    public required int OpenTotal { get; init; }

    public required DetectionLatencyDto Detection { get; init; }
}

public sealed record IncidentDayBucketDto
{
    public required DateOnly Day { get; init; }
    public required int Total { get; init; }

    /// <summary>
    /// Always carries every priority, including the zeroes. A stacked bar whose segments appear
    /// and disappear between days is unreadable, and filling the gaps on the client means every
    /// consumer has to know the enum.
    /// </summary>
    public required IReadOnlyDictionary<string, int> ByPriority { get; init; }
}

/// <summary>
/// The gap between when a problem started and when the incident was opened — the clearest single
/// number for "we noticed this rather than being told".
/// </summary>
public sealed record DetectionLatencyDto
{
    /// <summary>Incidents carrying a DetectedAt: the platform saw these itself.</summary>
    public required int NoticedCount { get; init; }

    /// <summary>Incidents with no DetectedAt: somebody filed these.</summary>
    public required int ToldCount { get; init; }

    /// <summary>
    /// Median rather than mean, and null when nothing was noticed. One incident opened days after
    /// its detection — a replay, a backfill, a clock problem — drags a mean far enough to make it
    /// a worse answer than no answer.
    /// </summary>
    public required double? MedianSeconds { get; init; }

    public required double? P95Seconds { get; init; }
}
