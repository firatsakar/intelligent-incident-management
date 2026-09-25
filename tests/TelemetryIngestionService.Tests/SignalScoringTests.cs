using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Tests;

// The gate that decides whether somebody is woken at 3am. It is a pure function, so it is cheap
// to test and silently wrong if it is not.
public sealed class SignalScoringTests
{
    // One organisation for the whole file. These are unit tests of rules, not of scoping — the
    // filters that make the column matter live in the DbContext — so the value only has to be
    // consistent.
    private static readonly Guid Organization = Guid.NewGuid();
    // Doubles are compared to ten decimal places throughout: the terms are summed, so 0.55 + 0.10
    // does not land exactly on 0.65 and an exact comparison would fail for the wrong reason.
    private const int Precision = 10;

    // A burst that merely clears the threshold, with nothing corroborating it.
    private static SignalScoring.Inputs BareBurst(long occurrences = 3, int threshold = 3) =>
        new()
        {
            Occurrences = occurrences,
            Threshold = threshold,
            ZScore = null,
            DistinctServices = 1,
            IsMuted = false,
            ConfirmedRealCount = 0,
            FalsePositiveCount = 0,
            IsFatal = false,
        };

    [Fact]
    public void Fatal_ScoresOneAndSkipsEveryOtherTerm()
    {
        // A crash is not a judgement call. Even a muted signature with a false-positive history
        // and no anomaly still scores 1.0 — none of those terms get a say.
        var inputs = BareBurst() with
        {
            IsFatal = true,
            IsMuted = true,
            FalsePositiveCount = 5,
        };

        var result = SignalScoring.Score(inputs);

        Assert.Equal(1.0, result.Confidence, Precision);
        Assert.Equal(new[] { "fatal" }, result.Breakdown.Keys);
    }

    [Fact]
    public void BareBurst_ScoresTheBaseAndNothingElse()
    {
        var result = SignalScoring.Score(BareBurst());

        Assert.Equal(SignalScoring.BurstBase, result.Confidence, Precision);
        Assert.Equal(SignalScoring.BurstBase, result.Breakdown["burstBase"], Precision);
        Assert.False(result.Breakdown.ContainsKey("overThreshold"));
        Assert.False(result.Breakdown.ContainsKey("rateAnomaly"));
    }

    [Theory]
    // Occurrences at the threshold are not *over* it.
    [InlineData(3, 3, null)]
    // Over, but not yet doubled — the first doubling is what earns the first step.
    [InlineData(4, 3, null)]
    [InlineData(6, 3, 0.10)]
    [InlineData(12, 3, 0.20)]
    [InlineData(24, 3, 0.30)]
    // Capped: a burst fifty times over is not five times worse again.
    [InlineData(48, 3, 0.30)]
    [InlineData(3000, 3, 0.30)]
    public void OverThresholdBonus_GrowsByDoublingsAndIsCapped(
        long occurrences,
        int threshold,
        double? expectedBonus
    )
    {
        var result = SignalScoring.Score(BareBurst(occurrences, threshold));

        if (expectedBonus is null)
        {
            Assert.False(result.Breakdown.ContainsKey("overThreshold"));
            Assert.Equal(SignalScoring.BurstBase, result.Confidence, Precision);

            return;
        }

        Assert.Equal(expectedBonus.Value, result.Breakdown["overThreshold"], Precision);
        Assert.Equal(SignalScoring.BurstBase + expectedBonus.Value, result.Confidence, Precision);
    }

    [Fact]
    public void ZeroThreshold_ContributesNoBonus()
    {
        // Guards the division. A rule with a zero threshold is a misconfiguration, not an
        // invitation to divide by it.
        var result = SignalScoring.Score(BareBurst(occurrences: 100, threshold: 0));

        Assert.False(result.Breakdown.ContainsKey("overThreshold"));
        Assert.Equal(SignalScoring.BurstBase, result.Confidence, Precision);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0.0, false)]
    [InlineData(1.99, false)]
    // The threshold is inclusive.
    [InlineData(2.0, true)]
    [InlineData(4.0, true)]
    public void RateAnomalyBonus_AppliesFromZScoreTwoUpwards(double? zScore, bool expected)
    {
        var result = SignalScoring.Score(BareBurst() with { ZScore = zScore });

        Assert.Equal(expected, result.Breakdown.ContainsKey("rateAnomaly"));

        if (expected)
            Assert.Equal(0.25, result.Breakdown["rateAnomaly"], Precision);
    }

    // The term that makes the system improve with use: the share of this signature's incidents
    // that were real, discounted while there are few verdicts.
    [Theory]
    [InlineData(1, 0, 0.05)]
    [InlineData(10, 0, 0.125)]
    [InlineData(9, 1, 0.0917)]
    [InlineData(5, 5, -0.0417)]
    [InlineData(0, 1, -0.0833)]
    [InlineData(0, 5, -0.1786)]
    public void History_ReadsTheShareOfRealVerdicts(int real, int falsePositive, double expected)
    {
        var result = SignalScoring.Score(
            BareBurst() with { ConfirmedRealCount = real, FalsePositiveCount = falsePositive }
        );

        Assert.Equal(expected, result.Breakdown["history"], 4);
        Assert.Equal(SignalScoring.BurstBase + expected, result.Confidence, 4);
    }

    [Fact]
    public void History_OneFalseAlarmDoesNotOutweighNineRealOnes()
    {
        // What the two flags it replaced got wrong: +0.15 and −0.25 together, −0.10 for ever.
        var history = SignalScoring.History(confirmedReal: 9, falsePositive: 1);

        Assert.NotNull(history);
        Assert.True(history > 0);
    }

    [Fact]
    public void History_StaysWithinItsBoundsHoweverManyVerdictsAccumulate()
    {
        // A record corroborates a burst; it must never be able to promote one on its own.
        var allReal = SignalScoring.History(confirmedReal: 100_000, falsePositive: 0);
        var allFalse = SignalScoring.History(confirmedReal: 0, falsePositive: 100_000);

        Assert.InRange(allReal!.Value, 0.149, SignalScoring.HistoryCeiling);
        Assert.InRange(allFalse!.Value, SignalScoring.HistoryFloor, -0.249);
    }

    [Fact]
    public void History_IsAbsentWithoutAVerdict()
    {
        // No record is not a neutral record: the breakdown says nothing rather than "0.00".
        var result = SignalScoring.Score(BareBurst());

        Assert.Null(SignalScoring.History(0, 0));
        Assert.False(result.Breakdown.ContainsKey("history"));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(7, true)]
    public void BlastRadius_AddsFromTwoDistinctServices(int distinctServices, bool expected)
    {
        var result = SignalScoring.Score(
            BareBurst() with
            {
                DistinctServices = distinctServices,
            }
        );

        Assert.Equal(expected, result.Breakdown.ContainsKey("blastRadius"));
    }

    [Fact]
    public void Muted_SubtractsEnoughToFallOutOfTheWeakBandOnItsOwn()
    {
        var result = SignalScoring.Score(BareBurst() with { IsMuted = true });

        Assert.Equal(-0.40, result.Breakdown["muted"], Precision);
        Assert.Equal(SignalScoring.BurstBase - 0.40, result.Confidence, Precision);
        Assert.True(result.Confidence < 0.60);
    }

    [Fact]
    public void Confidence_IsClampedToOne()
    {
        // Every positive term at once sums past 1.0.
        var inputs = BareBurst(occurrences: 3000, threshold: 3) with
        {
            ZScore = 9.0,
            DistinctServices = 4,
            ConfirmedRealCount = 3,
        };

        var result = SignalScoring.Score(inputs);

        Assert.Equal(1.0, result.Confidence, Precision);
        Assert.Equal(1.0, result.Breakdown["total"], Precision);
    }

    [Fact]
    public void Confidence_IsClampedToZero()
    {
        var inputs = BareBurst() with { IsMuted = true, FalsePositiveCount = 20 };

        var result = SignalScoring.Score(inputs);

        Assert.Equal(0.0, result.Confidence, Precision);
        Assert.Equal(0.0, result.Breakdown["total"], Precision);
    }

    [Fact]
    public void Breakdown_RecordsTheTotalAlongsideTheComponents()
    {
        // The breakdown is what the incident description quotes back as "why this was raised", so
        // the components have to add up to the total that was recorded. Kept clear of the clamp,
        // which is the one case where they legitimately disagree.
        var inputs = BareBurst(occurrences: 6, threshold: 3) with { ConfirmedRealCount = 1 };

        var result = SignalScoring.Score(inputs);

        var components = result
            .Breakdown.Where(entry => entry.Key != "total")
            .Sum(entry => entry.Value);

        Assert.Equal(result.Confidence, components, Precision);
        Assert.Equal(result.Confidence, result.Breakdown["total"], Precision);
    }

    public sealed class Bands
    {
        private static DetectionRule Rule(double promoteThreshold = 0.90) =>
            DetectionRule.Create(
                Organization,
                "test",
                service: null,
                LogSeverity.Error,
                windowSeconds: 300,
                threshold: 3,
                dedupWindowHours: 24,
                promoteThreshold: promoteThreshold
            );

        [Theory]
        [InlineData(0.89, false)]
        [InlineData(0.90, true)]
        [InlineData(1.00, true)]
        public void ShouldPromote_IsInclusiveAtTheThreshold(double confidence, bool expected)
        {
            Assert.Equal(expected, SignalScoring.ShouldPromote(confidence, Rule()));
        }

        [Theory]
        // Below 0.60 is recorded only — it still feeds the baseline and precedent, but no human
        // is shown it.
        [InlineData(0.55, false)]
        [InlineData(0.59, false)]
        [InlineData(0.60, true)]
        [InlineData(0.89, true)]
        // At and above the promotion threshold it is an incident, not a weak signal.
        [InlineData(0.90, false)]
        [InlineData(1.00, false)]
        public void IsWeak_CoversSixtyUpToButNotIncludingThePromotionThreshold(
            double confidence,
            bool expected
        )
        {
            Assert.Equal(expected, SignalScoring.IsWeak(confidence, Rule()));
        }

        [Fact]
        public void WeakBand_NarrowsWithTheRuleThreshold()
        {
            // The floor is fixed at 0.60 but the ceiling is the rule's, so tuning the promotion
            // threshold down shrinks the band rather than moving it.
            Assert.True(SignalScoring.IsWeak(0.70, Rule(promoteThreshold: 0.80)));
            Assert.False(SignalScoring.IsWeak(0.85, Rule(promoteThreshold: 0.80)));
        }

        [Fact]
        public void ADoubledBurstLandsInTheWeakBand()
        {
            // The band is reachable from real inputs, not just from arbitrary numbers: a burst at
            // twice the threshold with no corroboration scores 0.65 — worth showing someone, not
            // worth waking them.
            var inputs = BareBurst(occurrences: 6, threshold: 3);

            var result = SignalScoring.Score(inputs);

            Assert.Equal(0.65, result.Confidence, Precision);
            Assert.True(SignalScoring.IsWeak(result.Confidence, Rule()));
            Assert.False(SignalScoring.ShouldPromote(result.Confidence, Rule()));
        }

        [Fact]
        public void ADoubledBurstWithARateAnomalyIsPromoted()
        {
            // The same burst, once its own history says the rate is unusual, reaches exactly the
            // default promotion threshold.
            var inputs = BareBurst(occurrences: 6, threshold: 3) with { ZScore = 2.5 };

            var result = SignalScoring.Score(inputs);

            Assert.Equal(0.90, result.Confidence, Precision);
            Assert.True(SignalScoring.ShouldPromote(result.Confidence, Rule()));
        }
    }

    public sealed class Severity
    {
        [Fact]
        public void Fatal_IsCritical()
        {
            Assert.Equal("Critical", SignalScoring.SuggestSeverity(BareBurst() with { IsFatal = true }));
        }

        [Theory]
        [InlineData(29, "Medium")]
        // Ten times the threshold is where it steps up, inclusively.
        [InlineData(30, "High")]
        [InlineData(300, "High")]
        public void TenTimesTheThreshold_IsHigh(long occurrences, string expected)
        {
            Assert.Equal(expected, SignalScoring.SuggestSeverity(BareBurst(occurrences, threshold: 3)));
        }

        [Fact]
        public void ZeroThreshold_FallsBackToMedium()
        {
            Assert.Equal(
                "Medium",
                SignalScoring.SuggestSeverity(BareBurst(occurrences: 100, threshold: 0))
            );
        }
    }
}
