using IncidentService.Domain.Enums;

namespace IncidentService.Application.Abstractions;

/// <summary>
/// One incident, reduced to the five fields the dashboard actually counts.
///
/// The projection is the point. Bucketing happens in the handler rather than in SQL because
/// <c>CAST(timestamptz AS date)</c> resolves against the session's time zone, which is a
/// connection setting rather than something this query states — and a chart whose day boundaries
/// depend on how the pool was configured is a chart nobody can check. Grouping in memory makes
/// the boundary explicit and testable.
///
/// The cost of that choice is paid here: only five scalars cross the wire, never the title,
/// description or AI reasoning, which are the columns that would make a wide window expensive.
/// </summary>
public sealed record IncidentStatsRow(
    DateTime CreatedAt,
    DateTime? DetectedAt,
    IncidentPriority Priority,
    IncidentStatus Status,
    IncidentSource Source
);
