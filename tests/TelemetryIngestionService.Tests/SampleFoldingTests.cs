using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Tests;

// The storage decision of Adım 13.5: a signature's count and a few sample lines, not every line.
// Every rule below exists because getting it wrong silently changes what detection sees.
public sealed class SampleFoldingTests
{
    private static readonly DateTime Origin = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private static SampleFolding.Candidate Error(int second, string fingerprint = "storm") =>
        new(fingerprint, Origin.AddSeconds(second), IsFatal: false);

    private static List<SampleFolding.Candidate> Storm(int count, string fingerprint = "storm") =>
        Enumerable.Range(0, count).Select(second => Error(second, fingerprint)).ToList();

    [Fact]
    public void AStormKeepsTenRowsThatStillAddUpToEveryEvent()
    {
        // The whole point: 300 events, 10 rows, and a sum that detection reads as 300.
        var weights = SampleFolding.Weigh(Storm(300));

        Assert.Equal(SampleFolding.SamplesPerSignature, weights.Count(weight => weight > 0));
        Assert.Equal(300, weights.Sum());
    }

    [Fact]
    public void TheEarliestAreKeptAsTheyAreAndTheNewestCarriesTheRest()
    {
        // The count lands at the end of the burst, not before it: a window measured back from now
        // must not lose a storm's weight because it was parked on its first line.
        var weights = SampleFolding.Weigh(Storm(300));

        Assert.All(weights.Take(9), weight => Assert.Equal(1, weight));
        Assert.All(weights.Skip(9).SkipLast(1), weight => Assert.Equal(0, weight));
        Assert.Equal(291, weights[^1]);
    }

    [Fact]
    public void ASignatureAtOrUnderTheSampleSizeIsNotTouched()
    {
        Assert.All(SampleFolding.Weigh(Storm(10)), weight => Assert.Equal(1, weight));
    }

    [Fact]
    public void FatalIsNeverFolded()
    {
        // One crash is conclusive on its own; each one is its own evidence.
        var candidates = Storm(20);
        candidates.AddRange(
            Enumerable.Range(0, 15).Select(i => new SampleFolding.Candidate("storm", Origin.AddSeconds(i), IsFatal: true))
        );

        var weights = SampleFolding.Weigh(candidates);

        Assert.All(weights.Skip(20), weight => Assert.Equal(1, weight));
        Assert.Equal(35, weights.Sum());
    }

    [Fact]
    public void RecordsWithoutAFingerprintAreNeverFolded()
    {
        var context = Enumerable
            .Range(0, 50)
            .Select(i => new SampleFolding.Candidate(null, Origin.AddSeconds(i), IsFatal: false))
            .ToList();

        Assert.All(SampleFolding.Weigh(context), weight => Assert.Equal(1, weight));
    }

    [Fact]
    public void SignaturesAreFoldedIndependently()
    {
        var candidates = Storm(30, "a");
        candidates.AddRange(Storm(30, "b"));

        var weights = SampleFolding.Weigh(candidates);

        Assert.Equal(30, weights.Take(30).Sum());
        Assert.Equal(30, weights.Skip(30).Sum());
        Assert.Equal(20, weights.Count(weight => weight > 0));
    }

    [Fact]
    public void NothingAtTheBatchsNewestInstantIsFolded()
    {
        // A polling cursor re-reads the newest instant it has seen, on purpose, to leave no gap.
        // A re-read event is only recognised as already held if its row exists — fold it away and
        // the next poll counts it a second time.
        var candidates = Storm(20);
        var newest = Origin.AddMinutes(5);

        for (var i = 0; i < 5; i++)
            candidates.Add(new SampleFolding.Candidate("storm", newest, IsFatal: false));

        var weights = SampleFolding.Weigh(candidates);

        Assert.All(weights.Skip(20), weight => Assert.True(weight >= 1));
        Assert.Equal(25, weights.Sum());
    }

    [Fact]
    public void ArrivalOrderDoesNotChangeWhatIsKept()
    {
        // A source can deliver newest first — Seq does. The earliest events are the samples
        // whatever order they came in.
        var candidates = Storm(30);
        candidates.Reverse();

        var weights = SampleFolding.Weigh(candidates);

        Assert.Equal(30, weights.Sum());
        Assert.Equal(21, weights[0]);
        Assert.All(weights.TakeLast(9), weight => Assert.Equal(1, weight));
    }
}
