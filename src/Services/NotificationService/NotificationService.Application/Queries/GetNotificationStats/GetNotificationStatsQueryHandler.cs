using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Queries.GetNotificationStats;

public sealed class GetNotificationStatsQueryHandler
    : IRequestHandler<GetNotificationStatsQuery, NotificationStatsDto>
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(90);

    private readonly INotificationDeliveryRepository _deliveries;
    private readonly IIntegrationRepository _integrations;

    public GetNotificationStatsQueryHandler(
        INotificationDeliveryRepository deliveries,
        IIntegrationRepository integrations
    )
    {
        _deliveries = deliveries;
        _integrations = integrations;
    }

    public async Task<NotificationStatsDto> Handle(
        GetNotificationStatsQuery request,
        CancellationToken cancellationToken
    )
    {
        var to = request.To ?? DateTime.UtcNow;
        var from = request.From ?? to - DefaultWindow;

        if (to - from > MaxWindow)
            from = to - MaxWindow;

        var rows = await _deliveries.GetWindowAsync(from, to, cancellationToken);
        var integrations = await _integrations.GetAllAsync(cancellationToken);

        var byId = integrations.ToDictionary(integration => integration.Id);

        // Grouped by the delivery's integration id rather than by the configured integrations, so
        // a channel that was deleted this week still reports what it did before it went. Its
        // deliveries are the record that somebody was told, and that record outlives the config.
        var health = rows.GroupBy(row => row.IntegrationId)
            .Select(group => BuildHealth(group.Key, group.ToList(), byId))
            .OrderByDescending(entry => entry.Failed)
            .ThenBy(entry => entry.Name)
            .ToList();

        return new NotificationStatsDto
        {
            From = from,
            To = to,
            Integrations = health,
            TotalSent = rows.Count(row => row.Status == DeliveryStatus.Sent),
            TotalFailed = rows.Count(row => row.Status == DeliveryStatus.Failed),
            TotalPending = rows.Count(row => row.Status == DeliveryStatus.Pending),
        };
    }

    private static IntegrationHealthDto BuildHealth(
        Guid integrationId,
        IReadOnlyList<NotificationDelivery> rows,
        IReadOnlyDictionary<Guid, Integration> byId
    )
    {
        byId.TryGetValue(integrationId, out var integration);

        var lastFailure = rows.Where(row => row.Status == DeliveryStatus.Failed)
            .OrderByDescending(row => row.UpdatedAt ?? row.CreatedAt)
            .FirstOrDefault();

        var dispatchSeconds = rows.Where(row => row.SentAt.HasValue)
            .Select(row => (row.SentAt!.Value - row.CreatedAt).TotalSeconds)
            .Where(seconds => seconds >= 0)
            .OrderBy(seconds => seconds)
            .ToList();

        return new IntegrationHealthDto
        {
            IntegrationId = integrationId,
            Name = integration?.Name,
            Channel = integration?.Channel.ToString(),
            IsEnabled = integration?.IsEnabled ?? false,
            Sent = rows.Count(row => row.Status == DeliveryStatus.Sent),
            Failed = rows.Count(row => row.Status == DeliveryStatus.Failed),
            Pending = rows.Count(row => row.Status == DeliveryStatus.Pending),
            MedianDispatchSeconds = Median(dispatchSeconds),
            LastError = lastFailure?.LastError,
            LastErrorAt = lastFailure?.UpdatedAt ?? lastFailure?.CreatedAt,
            LastSentAt = rows.Where(row => row.SentAt.HasValue).Max(row => row.SentAt),
        };
    }

    /// <summary>
    /// Null on an empty list rather than zero. "Nothing was delivered" and "everything was
    /// delivered instantly" are opposite facts, and zero reports the wrong one.
    ///
    /// Nearest-rank, matching the detection latency in IncidentService: one definition of
    /// "median" across the product, and a reported duration that some delivery really took
    /// rather than an average of two that sits between them.
    /// </summary>
    private static double? Median(IReadOnlyList<double> sorted)
    {
        if (sorted.Count == 0)
            return null;

        var rank = (int)Math.Ceiling(0.50 * sorted.Count) - 1;

        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }
}
