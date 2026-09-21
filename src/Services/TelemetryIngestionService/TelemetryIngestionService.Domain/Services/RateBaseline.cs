namespace TelemetryIngestionService.Domain.Services;

// How unusual is this burst *for this signature*? A service that throws twenty errors an hour all
// day is not having an incident when it throws twenty more; a service that has thrown none all
// week very much is. Absolute thresholds cannot tell those apart — a baseline can.
public static class RateBaseline
{
    // Below this many observed windows there is no baseline worth the name, and calling something
    // anomalous against two data points is worse than saying nothing.
    public const int MinimumSamples = 5;

    // A perfectly flat history gives a standard deviation of zero, which would divide to
    // infinity. Any rise above a flat baseline is genuinely notable, so it is reported as a
    // strong-but-bounded score instead.
    private const double FlatBaselineScore = 4.0;

    private const double Epsilon = 1e-9;

    public static double? ZScore(IReadOnlyList<long> historicalCounts, long currentCount)
    {
        if (historicalCounts.Count < MinimumSamples)
            return null;

        var mean = historicalCounts.Average();

        var variance =
            historicalCounts.Sum(count => (count - mean) * (count - mean)) / historicalCounts.Count;

        var standardDeviation = Math.Sqrt(variance);

        if (standardDeviation < Epsilon)
            return currentCount > mean ? FlatBaselineScore : 0d;

        return (currentCount - mean) / standardDeviation;
    }

    // Splits a span of timestamps into fixed buckets and returns how many landed in each. The
    // final, still-open bucket is excluded: counting a partial window against complete ones would
    // make every signature look like it was calming down.
    public static IReadOnlyList<long> BucketCounts(
        IEnumerable<DateTime> timestamps,
        DateTime from,
        DateTime to,
        TimeSpan bucketSize
    )
    {
        if (bucketSize <= TimeSpan.Zero || to <= from)
            return [];

        var bucketCount = (int)Math.Floor((to - from) / bucketSize);

        if (bucketCount <= 0)
            return [];

        var buckets = new long[bucketCount];

        foreach (var timestamp in timestamps)
        {
            if (timestamp < from || timestamp >= to)
                continue;

            var index = (int)Math.Floor((timestamp - from) / bucketSize);

            if (index >= 0 && index < bucketCount)
                buckets[index]++;
        }

        return buckets;
    }
}
