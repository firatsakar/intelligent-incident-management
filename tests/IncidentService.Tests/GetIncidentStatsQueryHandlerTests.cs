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
        GivenOpen();
    }

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
}
