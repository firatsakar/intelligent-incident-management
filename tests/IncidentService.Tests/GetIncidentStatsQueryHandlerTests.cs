using IncidentService.Application.Abstractions;
using IncidentService.Application.Queries.GetIncidentStats;
using IncidentService.Domain.Enums;
using NSubstitute;

namespace IncidentService.Tests;

// The dashboard's only source of shape. Most of what can go wrong here is arithmetic that still
// renders: a missing day silently compresses the time axis, a zero median reports the opposite of
// "nothing was noticed automatically", and a negative gap from two disagreeing clocks would pull
// the latency toward a number that never happened.
public sealed class GetIncidentStatsQueryHandlerTests
{
    private static readonly DateTime Noon = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly IIncidentRepository _repository = Substitute.For<IIncidentRepository>();
    private readonly GetIncidentStatsQueryHandler _handler;

    public GetIncidentStatsQueryHandlerTests()
    {
        _handler = new GetIncidentStatsQueryHandler(_repository);

        GivenRows();
        GivenResolved();
        GivenOpen();
    }

    private void GivenResolved(params IncidentResolutionRow[] rows)
    {
        IReadOnlyList<IncidentResolutionRow> result = rows;

        _repository
            .GetResolvedRowsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private static IncidentResolutionRow Closed(
        DateTime resolvedAt,
        TimeSpan took,
        IncidentPriority priority = IncidentPriority.High,
        IncidentSource source = IncidentSource.Telemetry,
        IncidentVerdict? verdict = IncidentVerdict.Real,
        bool detected = true
    ) =>
        detected
            ? new(resolvedAt - took - TimeSpan.FromMinutes(3), resolvedAt - took, resolvedAt, priority, source, verdict)
            : new(resolvedAt - took, null, resolvedAt, priority, source, verdict);

    private void GivenRows(params IncidentStatsRow[] rows)
    {
        IReadOnlyList<IncidentStatsRow> result = rows;

        _repository
            .GetStatsRowsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(result);
    }

    private void GivenOpen(params (IncidentPriority Priority, int Count)[] counts)
    {
        IReadOnlyDictionary<IncidentPriority, int> result = counts.ToDictionary(
            pair => pair.Priority,
            pair => pair.Count
        );

        _repository.GetOpenCountsByPriorityAsync(Arg.Any<CancellationToken>()).Returns(result);
    }

    private static IncidentStatsRow Row(
        DateTime createdAt,
        IncidentPriority priority = IncidentPriority.High,
        IncidentSource source = IncidentSource.Telemetry,
        IncidentStatus status = IncidentStatus.Open,
        DateTime? detectedAt = null
    ) => new(createdAt, detectedAt, priority, status, source);

    [Fact]
    public async Task EveryDayInTheWindowGetsABucket_IncludingTheQuietOnes()
    {
        // A chart that skips its empty days compresses time, and a burst then looks like the
        // normal rate. Four days in, four buckets out, even though only one has anything in it.
        GivenRows(Row(Noon));

        var stats = await _handler.Handle(
            new GetIncidentStatsQuery(Noon.AddDays(-3), Noon),
            CancellationToken.None
        );

        Assert.Equal(4, stats.Days.Count);
        Assert.Equal(3, stats.Days.Count(day => day.Total == 0));
        Assert.Single(stats.Days, day => day.Total == 1);
    }

    [Fact]
    public async Task EveryBucketCarriesEveryPriority_EvenTheEmptyOnes()
    {
        // Stacked segments that appear and disappear between days cannot be compared by eye, and
        // filling the gaps client-side would push the enum's shape onto every consumer.
        GivenRows(Row(Noon, IncidentPriority.Critical));

        var stats = await _handler.Handle(
            new GetIncidentStatsQuery(Noon.AddDays(-1), Noon),
            CancellationToken.None
        );

        Assert.All(
            stats.Days,
            day => Assert.Equal(Enum.GetValues<IncidentPriority>().Length, day.ByPriority.Count)
        );
        Assert.Equal(0, stats.Days[0].ByPriority["Critical"]);
        Assert.Equal(1, stats.Days[1].ByPriority["Critical"]);
    }

    [Fact]
    public async Task RowsAreBucketedByUtcDay()
    {
        // 23:30 and 00:30 are an hour apart and belong to different buckets. This is the whole
        // reason the boundary is stated rather than inherited from a connection's time zone.
        var lateNight = new DateTime(2026, 9, 21, 23, 30, 0, DateTimeKind.Utc);

        GivenRows(Row(lateNight), Row(lateNight.AddHours(1)));

        var stats = await _handler.Handle(
            new GetIncidentStatsQuery(lateNight.AddHours(-2), lateNight.AddHours(2)),
            CancellationToken.None
        );

        Assert.Equal(2, stats.Days.Count);
        Assert.All(stats.Days, day => Assert.Equal(1, day.Total));
    }

    [Fact]
    public async Task OpenCountsIgnoreTheWindow()
    {
        // An incident opened six weeks ago and still open is the one an operator most needs, and
        // a date filter is exactly what would hide it. The repository is asked without a range.
        GivenOpen((IncidentPriority.Critical, 2), (IncidentPriority.Low, 1));

        var stats = await _handler.Handle(
            new GetIncidentStatsQuery(Noon.AddDays(-1), Noon),
            CancellationToken.None
        );

        Assert.Equal(3, stats.OpenTotal);
        Assert.Equal(2, stats.OpenByPriority["Critical"]);
        // Still present, still zero — the client never has to know the enum.
        Assert.Equal(0, stats.OpenByPriority["High"]);
    }

    [Fact]
    public async Task DetectionSplitsWhatWasNoticedFromWhatWasReported()
    {
        GivenRows(
            Row(Noon, detectedAt: Noon.AddMinutes(-2)),
            Row(Noon, detectedAt: Noon.AddMinutes(-4)),
            Row(Noon)
        );

        var stats = await _handler.Handle(
            new GetIncidentStatsQuery(Noon.AddDays(-1), Noon),
            CancellationToken.None
        );

        Assert.Equal(2, stats.Detection.NoticedCount);
        Assert.Equal(1, stats.Detection.ToldCount);

        // Nearest-rank, so an even-sized sample reports the lower of the two middle values rather
        // than an average of them. The reported number is therefore always a latency that really
        // happened, which is what makes it safe to put next to a P95.
        Assert.Equal(120, stats.Detection.MedianSeconds);
    }

    [Fact]
    public async Task NothingNoticedMeansNoLatency_NotZeroLatency()
    {
        // Zero would read as "noticed instantly", which is the opposite of what happened.
        GivenRows(Row(Noon), Row(Noon));

        var stats = await _handler.Handle(
            new GetIncidentStatsQuery(Noon.AddDays(-1), Noon),
            CancellationToken.None
        );

        Assert.Null(stats.Detection.MedianSeconds);
        Assert.Null(stats.Detection.P95Seconds);
        Assert.Equal(2, stats.Detection.ToldCount);
    }

    [Fact]
    public async Task AClockDisagreementDoesNotDragTheMedianBelowZero()
    {
        // DetectedAt after CreatedAt means the two clocks disagree, not that the incident was
        // opened before the problem started. It still counts as noticed; it just cannot be a
        // latency, so it sits out of the percentiles instead of inventing a negative one.
        GivenRows(
            Row(Noon, detectedAt: Noon.AddMinutes(1)),
            Row(Noon, detectedAt: Noon.AddMinutes(-2))
        );

        var stats = await _handler.Handle(
            new GetIncidentStatsQuery(Noon.AddDays(-1), Noon),
            CancellationToken.None
        );

        Assert.Equal(2, stats.Detection.NoticedCount);
        Assert.Equal(120, stats.Detection.MedianSeconds);
    }

    [Fact]
    public async Task AnOverwideWindowIsClampedRatherThanRejected()
    {
        // A caller asking for more than this endpoint promises is not a malformed request, and a
        // 400 is something the dashboard cannot act on.
        await _handler.Handle(
            new GetIncidentStatsQuery(Noon.AddYears(-10), Noon),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .GetStatsRowsAsync(
                Noon - GetIncidentStatsQueryHandler.MaxWindow,
                Noon,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NoWindowMeansTheLastThirtyDays()
    {
        var stats = await _handler.Handle(new GetIncidentStatsQuery(), CancellationToken.None);

        Assert.Equal(
            GetIncidentStatsQueryHandler.DefaultWindow.Days,
            (int)Math.Round((stats.To - stats.From).TotalDays)
        );
    }

    [Fact]
    public async Task CategoricalSplitsCountTheWholeWindow()
    {
        GivenRows(
            Row(Noon, IncidentPriority.Critical, IncidentSource.Telemetry),
            Row(Noon, IncidentPriority.Low, IncidentSource.Manual),
            Row(Noon, IncidentPriority.Low, IncidentSource.Manual, IncidentStatus.Resolved)
        );

        var stats = await _handler.Handle(
            new GetIncidentStatsQuery(Noon.AddDays(-1), Noon),
            CancellationToken.None
        );

        Assert.Equal(3, stats.Total);
        Assert.Equal(2, stats.ByPriority["Low"]);
        Assert.Equal(2, stats.BySource["Manual"]);
        Assert.Equal(1, stats.ByStatus["Resolved"]);
    }
    // ---- Adım 20.8 --------------------------------------------------------------------------------

    private Task<IncidentService.Application.DTOs.IncidentStatsDto> Window() =>
        _handler.Handle(new GetIncidentStatsQuery(Noon.AddDays(-7), Noon), CancellationToken.None);

    [Fact]
    public async Task ResolutionTimeRunsFromWhenTheProblemStartedToWhenItClosed()
    {
        GivenResolved(
            // Detected: timed from DetectedAt, not from when the record was filed three minutes later.
            Closed(Noon.AddHours(-1), TimeSpan.FromMinutes(30)),
            // Opened by hand: nothing detected it, so the clock starts at CreatedAt.
            Closed(Noon.AddHours(-2), TimeSpan.FromMinutes(90), detected: false),
            Closed(Noon.AddHours(-3), TimeSpan.FromMinutes(60), priority: IncidentPriority.Low)
        );

        var stats = await Window();

        Assert.Equal(3, stats.Resolution.ResolvedCount);
        Assert.Equal(3600, stats.Resolution.MedianSeconds);
        Assert.Equal(5400, stats.Resolution.P95Seconds);
        // Nearest-rank, as everywhere on this screen: of two, the lower — a duration that happened.
        Assert.Equal(1800, stats.Resolution.MedianSecondsByPriority["High"]);
        Assert.Equal(3600, stats.Resolution.MedianSecondsByPriority["Low"]);
        Assert.Null(stats.Resolution.MedianSecondsByPriority["Critical"]);
    }

    [Fact]
    public async Task NothingClosedIsNoAnswerRatherThanAnInstantOne()
    {
        var stats = await Window();

        Assert.Equal(0, stats.Resolution.ResolvedCount);
        Assert.Null(stats.Resolution.MedianSeconds);
        Assert.Null(stats.Resolution.P95Seconds);
    }

    [Fact]
    public async Task AClosureBeforeItsOwnDetectionCountsButSitsOutOfTheTimes()
    {
        GivenResolved(
            Closed(Noon, TimeSpan.FromMinutes(10)),
            // Two clocks disagreeing: closed "before" the problem started.
            Closed(Noon, TimeSpan.FromMinutes(-5))
        );

        var stats = await Window();

        Assert.Equal(2, stats.Resolution.ResolvedCount);
        Assert.Equal(600, stats.Resolution.MedianSeconds);
    }

    [Fact]
    public async Task VerdictsAreCountedBySourceWithEverySourcePresent()
    {
        GivenResolved(
            Closed(Noon, TimeSpan.FromMinutes(5), source: IncidentSource.Telemetry, verdict: IncidentVerdict.Real),
            Closed(Noon, TimeSpan.FromMinutes(5), source: IncidentSource.Telemetry, verdict: IncidentVerdict.Real),
            Closed(Noon, TimeSpan.FromMinutes(5), source: IncidentSource.Telemetry, verdict: IncidentVerdict.FalsePositive),
            Closed(Noon, TimeSpan.FromMinutes(5), source: IncidentSource.Alert, verdict: null)
        );

        var stats = await Window();

        Assert.Equal(2, stats.Verdicts["Telemetry"].Real);
        Assert.Equal(1, stats.Verdicts["Telemetry"].FalsePositive);
        Assert.Equal(1, stats.Verdicts["Alert"].Unknown);
        Assert.Equal(0, stats.Verdicts["Manual"].Real + stats.Verdicts["Manual"].FalsePositive + stats.Verdicts["Manual"].Unknown);
    }

    [Fact]
    public async Task AnalysesAreAnalysedFailedOrPending_AndALateSuccessIsAnalysed()
    {
        GivenRows(
            Row(Noon) with { IsAiAnalyzed = true, AiConfidence = 0.9 },
            Row(Noon) with { IsAiAnalyzed = true, AiConfidence = 0.6 },
            Row(Noon) with { IsAiAnalyzed = true, AiConfidence = 0.8, AiFailed = true },
            Row(Noon) with { AiFailed = true },
            Row(Noon)
        );

        var stats = await Window();

        Assert.Equal(3, stats.Ai.Analysed);
        Assert.Equal(1, stats.Ai.Failed);
        Assert.Equal(1, stats.Ai.Pending);
        Assert.Equal(0.8, stats.Ai.MedianConfidence);
    }

    [Fact]
    public async Task EachDayCountsWhatClosedThatDay_WheneverItWasOpened()
    {
        GivenResolved(
            // Opened weeks before the window, closed inside it: still today's closure.
            Closed(Noon, TimeSpan.FromDays(40)),
            Closed(Noon.AddDays(-1), TimeSpan.FromHours(2)),
            Closed(Noon.AddDays(-1), TimeSpan.FromHours(1))
        );

        var stats = await Window();

        Assert.Equal(1, stats.Days.Single(day => day.Day == DateOnly.FromDateTime(Noon)).Resolved);
        Assert.Equal(2, stats.Days.Single(day => day.Day == DateOnly.FromDateTime(Noon.AddDays(-1))).Resolved);
        Assert.Equal(0, stats.Days.Single(day => day.Day == DateOnly.FromDateTime(Noon.AddDays(-2))).Resolved);
    }
}
