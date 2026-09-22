using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Enums;
using MediatR;

namespace IncidentService.Application.Queries.GetIncidentStats;

public sealed class GetIncidentStatsQueryHandler
    : IRequestHandler<GetIncidentStatsQuery, IncidentStatsDto>
{
    /// <summary>The range a dashboard opens on when the caller does not say.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(30);

    /// <summary>
    /// A ceiling rather than a guess: the query projects five scalars per row, so a year of a
    /// busy system is still a modest read, and anything past a year is a reporting job rather
    /// than a dashboard. Without a ceiling this endpoint has the same unbounded shape as the
    /// incident list, which is the thing it exists to avoid.
    /// </summary>
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(366);

    private readonly IIncidentRepository _repository;

    public GetIncidentStatsQueryHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IncidentStatsDto> Handle(
        GetIncidentStatsQuery request,
        CancellationToken cancellationToken
    )
    {
        var to = request.To ?? DateTime.UtcNow;
        var from = request.From ?? to - DefaultWindow;

        // Clamped rather than rejected. A window too wide is a caller asking for more than this
        // endpoint promises, not a malformed request, and silently returning a year of data is
        // more useful than a 400 the dashboard cannot act on.
        if (to - from > MaxWindow)
            from = to - MaxWindow;

        var rows = await _repository.GetStatsRowsAsync(from, to, cancellationToken);
        var openByPriority = await _repository.GetOpenCountsByPriorityAsync(cancellationToken);

        return new IncidentStatsDto
        {
            From = from,
            To = to,
            Days = BuildDays(rows, from, to),
            ByPriority = CountBy(rows, row => row.Priority),
            ByStatus = CountBy(rows, row => row.Status),
            BySource = CountBy(rows, row => row.Source),
            OpenByPriority = FillZeroes(
                openByPriority.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                Enum.GetNames<IncidentPriority>()
            ),
            Total = rows.Count,
            OpenTotal = openByPriority.Values.Sum(),
            Detection = BuildDetection(rows),
        };
    }

    /// <summary>
    /// One bucket per UTC day across the whole window, empty days included. A chart that skips
    /// its quiet days compresses time and makes a burst look like the normal rate.
    /// </summary>
    private static List<IncidentDayBucketDto> BuildDays(
        IReadOnlyList<IncidentStatsRow> rows,
        DateTime from,
        DateTime to
    )
    {
        var priorities = Enum.GetNames<IncidentPriority>();

        var byDay = rows.GroupBy(row => DateOnly.FromDateTime(row.CreatedAt.ToUniversalTime()))
            .ToDictionary(group => group.Key, group => group.ToList());

        var firstDay = DateOnly.FromDateTime(from.ToUniversalTime());
        var lastDay = DateOnly.FromDateTime(to.ToUniversalTime());

        var days = new List<IncidentDayBucketDto>();

        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            byDay.TryGetValue(day, out var inDay);

            days.Add(
                new IncidentDayBucketDto
                {
                    Day = day,
                    Total = inDay?.Count ?? 0,
                    ByPriority = FillZeroes(
                        CountBy(inDay ?? [], row => row.Priority),
                        priorities
                    ),
                }
            );
        }

        return days;
    }

    private static DetectionLatencyDto BuildDetection(IReadOnlyList<IncidentStatsRow> rows)
    {
        // A negative gap means the two clocks disagree, not that the incident was opened before
        // the problem started. Counting it as a latency of "less than nothing" would pull the
        // median toward a number that never happened, so those rows sit out of the percentiles
        // while still counting as noticed.
        var latencies = rows.Where(row => row.DetectedAt.HasValue)
            .Select(row => (row.CreatedAt - row.DetectedAt!.Value).TotalSeconds)
            .Where(seconds => seconds >= 0)
            .OrderBy(seconds => seconds)
            .ToList();

        return new DetectionLatencyDto
        {
            NoticedCount = rows.Count(row => row.DetectedAt.HasValue),
            ToldCount = rows.Count(row => !row.DetectedAt.HasValue),
            MedianSeconds = Percentile(latencies, 0.50),
            P95Seconds = Percentile(latencies, 0.95),
        };
    }

    /// <summary>
    /// Nearest-rank on an already sorted list. Null for an empty list, because "no incident was
    /// noticed automatically" and "it was noticed instantly" are opposite facts and zero would
    /// report the wrong one.
    /// </summary>
    private static double? Percentile(IReadOnlyList<double> sorted, double fraction)
    {
        if (sorted.Count == 0)
            return null;

        var rank = (int)Math.Ceiling(fraction * sorted.Count) - 1;

        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }

    private static Dictionary<string, int> CountBy<TKey>(
        IEnumerable<IncidentStatsRow> rows,
        Func<IncidentStatsRow, TKey> selector
    )
        where TKey : notnull
    {
        return rows.GroupBy(selector)
            .ToDictionary(group => group.Key.ToString()!, group => group.Count());
    }

    /// <summary>
    /// Every member of the enum appears, including the ones with nothing in them. Leaving a key
    /// out pushes the enum's shape onto every consumer, and a stacked bar with segments that
    /// come and go between days cannot be read.
    /// </summary>
    private static Dictionary<string, int> FillZeroes(
        Dictionary<string, int> counts,
        IReadOnlyList<string> allKeys
    )
    {
        foreach (var key in allKeys)
            counts.TryAdd(key, 0);

        return counts;
    }
}
