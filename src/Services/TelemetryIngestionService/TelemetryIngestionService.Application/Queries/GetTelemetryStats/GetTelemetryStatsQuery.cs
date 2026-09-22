using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Queries.GetTelemetryStats;

/// <summary>
/// Both ends are optional; left out, the window is the last twenty-four hours ending now. Shorter
/// than the incident dashboard's default on purpose — telemetry is high-volume and the question
/// it answers ("what is the gate doing right now") has a shorter useful memory than "how have
/// incidents arrived this month".
/// </summary>
public sealed record GetTelemetryStatsQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<TelemetryStatsDto>;
