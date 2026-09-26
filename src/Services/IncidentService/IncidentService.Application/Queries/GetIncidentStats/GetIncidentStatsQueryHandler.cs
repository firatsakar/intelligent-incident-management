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
        var resolved = await _repository.GetResolvedRowsAsync(from, to, cancellationToken);
        var openByPriority = await _repository.GetOpenCountsByPriorityAsync(cancellationToken);

        return new IncidentStatsDto
        {
            From = from,
            To = to,
            Days = BuildDays(rows, resolved, from, to),
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
            Resolution = BuildResolution(resolved),
            Verdicts = BuildVerdicts(resolved),
            Ai = BuildAi(rows),
        };
    }

    /// <summary>
    /// One bucket per UTC day across the whole window, empty days included. A chart that skips
    /// its quiet days compresses time and makes a burst look like the normal rate.
    /// </summary>
    private static List<IncidentDayBucketDto> BuildDays(
        IReadOnlyList<IncidentStatsRow> rows,
        IReadOnlyList<IncidentResolutionRow> resolved,
        DateTime from,
        DateTime to
    )
    {
        var priorities = Enum.GetNames<IncidentPriority>();

        var byDay = rows.GroupBy(row => DateOnly.FromDateTime(row.CreatedAt.ToUniversalTime()))
            .ToDictionary(group => group.Key, group => group.ToList());

        var resolvedByDay = resolved
            .GroupBy(row => DateOnly.FromDateTime(row.ResolvedAt.ToUniversalTime()))
            .ToDictionary(group => group.Key, group => group.Count());

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
                    Resolved = resolvedByDay.GetValueOrDefault(day),
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

    // ---- Adım 20.8 ---------------------------------------------------------------------------

    private static double ResolutionSeconds(IncidentResolutionRow row) =>
        (row.ResolvedAt - (row.DetectedAt ?? row.CreatedAt)).TotalSeconds;

    // Same stance as detection latency: a negative duration is two clocks disagreeing, not an
    // incident closed before its problem began. It counts as closed and sits out of the times.
    private static List<double> SortedDurations(IEnumerable<IncidentResolutionRow> rows) =>
        rows.Select(ResolutionSeconds).Where(seconds => seconds >= 0).OrderBy(seconds => seconds).ToList();

    private static ResolutionDto BuildResolution(IReadOnlyList<IncidentResolutionRow> resolved)
    {
        var all = SortedDurations(resolved);

        return new ResolutionDto
        {
            ResolvedCount = resolved.Count,
            MedianSeconds = Percentile(all, 0.50),
            P95Seconds = Percentile(all, 0.95),
            MedianSecondsByPriority = Enum.GetValues<IncidentPriority>()
                .ToDictionary(
                    priority => priority.ToString(),
                    priority => Percentile(SortedDurations(resolved.Where(row => row.Priority == priority)), 0.50)
                ),
        };
    }

    private static Dictionary<string, VerdictCountsDto> BuildVerdicts(IReadOnlyList<IncidentResolutionRow> resolved) =>
        Enum.GetValues<IncidentSource>()
            .ToDictionary(
                source => source.ToString(),
                source =>
                {
                    var fromSource = resolved.Where(row => row.Source == source).ToList();

                    return new VerdictCountsDto
                    {
                        Real = fromSource.Count(row => row.Verdict == IncidentVerdict.Real),
                        FalsePositive = fromSource.Count(row => row.Verdict == IncidentVerdict.FalsePositive),
                        Unknown = fromSource.Count(row => row.Verdict is null),
                    };
                }
            );

    private static AiAnalysisDto BuildAi(IReadOnlyList<IncidentStatsRow> rows)
    {
        // A late success clears an earlier failure (Incident.ApplyAiAnalysis), so analysed wins.
        var analysed = rows.Where(row => row.IsAiAnalyzed).ToList();
        var failed = rows.Count(row => !row.IsAiAnalyzed && row.AiFailed);

        var confidences = analysed
            .Where(row => row.AiConfidence.HasValue)
            .Select(row => row.AiConfidence!.Value)
            .OrderBy(confidence => confidence)
            .ToList();

        return new AiAnalysisDto
        {
            Analysed = analysed.Count,
            Failed = failed,
            Pending = rows.Count - analysed.Count - failed,
            MedianConfidence = Percentile(confidences, 0.50),
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
