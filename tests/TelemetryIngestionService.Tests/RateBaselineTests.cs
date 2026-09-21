using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Tests;

// How unusual a burst is *for this signature*. The component that lets a service which throws
// twenty errors an hour all day be told apart from one that has thrown none all week.
public sealed class RateBaselineTests
{
    private const int Precision = 10;

    private static readonly DateTime Origin = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    public sealed class ZScore
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(4)]
        public void FewerThanFiveSamples_HasNoBaselineWorthTheName(int sampleCount)
        {
            var history = Enumerable.Repeat(10L, sampleCount).ToArray();

            // Null rather than zero: "no opinion" and "not anomalous" are different answers, and
            // the scorer only awards the anomaly bonus for the second.
            Assert.Null(RateBaseline.ZScore(history, currentCount: 500));
        }

        [Fact]
        public void FiveSamples_IsEnough()
        {
            Assert.NotNull(RateBaseline.ZScore([10, 10, 10, 10, 10], currentCount: 20));
        }

        [Fact]
        public void FlatBaseline_ReportsABoundedScoreRatherThanDividingByZero()
        {
            // A perfectly flat history has a standard deviation of zero. Any rise above it is
            // genuinely notable, so it is reported as a strong-but-bounded 4.0.
            var result = RateBaseline.ZScore([10, 10, 10, 10, 10], currentCount: 11);

            Assert.Equal(4.0, result!.Value, Precision);
        }

        [Fact]
        public void FlatBaselineAtZero_StillReportsTheBoundedScore()
        {
            // The case the whole component exists for: a signature that has never fired before
            // and has just fired ten times.
            var result = RateBaseline.ZScore([0, 0, 0, 0, 0, 0], currentCount: 10);

            Assert.Equal(4.0, result!.Value, Precision);
        }

        [Theory]
        // Matching the flat baseline is not a rise.
        [InlineData(10)]
        [InlineData(3)]
        [InlineData(0)]
        public void FlatBaseline_ScoresZeroWhenTheCurrentCountDoesNotRise(long currentCount)
        {
            var result = RateBaseline.ZScore([10, 10, 10, 10, 10], currentCount);

            Assert.Equal(0d, result!.Value, Precision);
        }

        [Fact]
        public void VariedBaseline_UsesTheStandardDeviation()
        {
            // mean 3, population variance 2, standard deviation √2 — so 10 sits (10-3)/√2 above.
            var result = RateBaseline.ZScore([1, 2, 3, 4, 5], currentCount: 10);

            Assert.Equal(7 / Math.Sqrt(2), result!.Value, Precision);
        }

        [Fact]
        public void ACountInsideANoisyBaseline_IsNotAnomalous()
        {
            // The false-positive case worth pinning: a service that is always noisy does not get
            // an anomaly bonus for being noisy again.
            var result = RateBaseline.ZScore([80, 120, 95, 110, 105, 90], currentCount: 100);

            Assert.True(result!.Value < 2.0);
        }

        [Fact]
        public void ADropBelowTheBaseline_ScoresNegative()
        {
            var result = RateBaseline.ZScore([1, 2, 3, 4, 5], currentCount: 0);

            Assert.True(result!.Value < 0);
        }
    }

    public sealed class BucketCounts
    {
        [Fact]
        public void SplitsTimestampsIntoFixedWindows()
        {
            DateTime[] timestamps =
            [
                Origin.AddSeconds(10),
                Origin.AddSeconds(20),
                Origin.AddMinutes(1).AddSeconds(5),
                Origin.AddMinutes(3),
            ];

            var buckets = RateBaseline.BucketCounts(
                timestamps,
                Origin,
                Origin.AddMinutes(4),
                TimeSpan.FromMinutes(1)
            );

            Assert.Equal(new long[] { 2, 1, 0, 1 }, buckets);
        }

        [Fact]
        public void ExcludesTheStillOpenFinalWindow()
        {
            // The reason this matters: counting a partial window against complete ones makes
            // every signature look like it is calming down. Four and a half minutes of range at
            // one minute a bucket yields four buckets, and the half-minute tail is dropped.
            DateTime[] timestamps =
            [
                Origin.AddSeconds(30),
                // Inside the incomplete fifth window.
                Origin.AddMinutes(4).AddSeconds(10),
                Origin.AddMinutes(4).AddSeconds(20),
            ];

            var buckets = RateBaseline.BucketCounts(
                timestamps,
                Origin,
                Origin.AddMinutes(4).AddSeconds(30),
                TimeSpan.FromMinutes(1)
            );

            Assert.Equal(new long[] { 1, 0, 0, 0 }, buckets);
        }

        [Fact]
        public void ExcludesTimestampsOutsideTheRange()
        {
            DateTime[] timestamps =
            [
                Origin.AddSeconds(-1),
                Origin,
                // The upper bound is exclusive.
                Origin.AddMinutes(2),
                Origin.AddMinutes(5),
            ];

            var buckets = RateBaseline.BucketCounts(
                timestamps,
                Origin,
                Origin.AddMinutes(2),
                TimeSpan.FromMinutes(1)
            );

            Assert.Equal(new long[] { 1, 0 }, buckets);
        }

        [Fact]
        public void RangeShorterThanOneBucket_YieldsNothing()
        {
            var buckets = RateBaseline.BucketCounts(
                [Origin.AddSeconds(10)],
                Origin,
                Origin.AddSeconds(30),
                TimeSpan.FromMinutes(1)
            );

            Assert.Empty(buckets);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-60)]
        public void NonPositiveBucketSize_YieldsNothing(int bucketSeconds)
        {
            var buckets = RateBaseline.BucketCounts(
                [Origin],
                Origin,
                Origin.AddMinutes(10),
                TimeSpan.FromSeconds(bucketSeconds)
            );

            Assert.Empty(buckets);
        }

        [Fact]
        public void InvertedRange_YieldsNothing()
        {
            var buckets = RateBaseline.BucketCounts(
                [Origin],
                Origin.AddMinutes(10),
                Origin,
                TimeSpan.FromMinutes(1)
            );

            Assert.Empty(buckets);
        }

        [Fact]
        public void NoTimestamps_YieldsZeroedBucketsRatherThanNothing()
        {
            // A quiet signature has a baseline of zeroes, which is exactly what makes the next
            // burst anomalous. Returning an empty list instead would drop below the minimum
            // sample count and silently suppress the anomaly bonus.
            var buckets = RateBaseline.BucketCounts(
                [],
                Origin,
                Origin.AddMinutes(6),
                TimeSpan.FromMinutes(1)
            );

            Assert.Equal(new long[] { 0, 0, 0, 0, 0, 0 }, buckets);
            Assert.Equal(4.0, RateBaseline.ZScore(buckets, currentCount: 30)!.Value, Precision);
        }
    }
}
