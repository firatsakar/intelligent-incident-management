using IncidentService.Application.DTOs;
using MediatR;

namespace IncidentService.Application.Queries.GetIncidentStats;

/// <summary>
/// Both ends are optional. Left out, the window is the last thirty days ending now — the range
/// a dashboard opens on.
/// </summary>
public sealed record GetIncidentStatsQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<IncidentStatsDto>;
