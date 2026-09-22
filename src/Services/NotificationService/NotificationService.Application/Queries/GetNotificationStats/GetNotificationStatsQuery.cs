using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetNotificationStats;

/// <summary>
/// Both ends optional; left out, the window is the last seven days ending now. Long enough that a
/// channel which failed once at the weekend is still on the screen on Monday.
/// </summary>
public sealed record GetNotificationStatsQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<NotificationStatsDto>;
