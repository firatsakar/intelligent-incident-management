using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Tests;

public sealed class LogRecordTests
{
    // One organisation for the whole file. These are unit tests of rules, not of scoping — the
    // filters that make the column matter live in the DbContext — so the value only has to be
    // consistent.
    private static readonly Guid Organization = Guid.NewGuid();
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(2);

    private static LogRecord Create(DateTime timestamp, string? fingerprint = "abc123") =>
        LogRecord.Create(
            Organization,
            Guid.NewGuid(),
            sourceEventId: "event-1",
            service: "checkout-service",
            severity: LogSeverity.Error,
            message: "Payment for order 4471 failed",
            normalizedMessage: "Payment for order {OrderId} failed",
            exceptionType: "TimeoutException",
            stackTrace: null,
            fingerprint: fingerprint,
            timestamp: timestamp,
            clockSkewTolerance: Tolerance
        );

    [Fact]
    public void TheSourceClockAndOurClockAreKeptApart()
    {
        // The two timestamps are the foundation of the correlation story and are never conflated.
        // The gap between them is ingestion lag, and it is only visible while both are recorded.
        var timestamp = DateTime.UtcNow.AddMinutes(-30);

        var record = Create(timestamp);

        Assert.Equal(timestamp, record.Timestamp);
        Assert.NotEqual(record.Timestamp, record.IngestedAt);
        Assert.True(record.IngestedAt > record.Timestamp);
    }

    [Theory]
    [InlineData(-3600)]
    [InlineData(-1)]
    [InlineData(0)]
    // Inside the tolerance this is latency, not a broken clock.
    [InlineData(60)]
    [InlineData(110)]
    public void TimestampsWithinToleranceAreNotSkew(int offsetSeconds)
    {
        var record = Create(DateTime.UtcNow.AddSeconds(offsetSeconds));

        Assert.False(record.HasClockSkew);
    }

    [Theory]
    [InlineData(180)]
    [InlineData(86_400)]
    public void TimestampsBeyondToleranceAreFlaggedRatherThanRejected(int offsetSeconds)
    {
        // Recorded, not dropped: a misconfigured source clock should be visible to an operator
        // instead of silently distorting every detection window it touches.
        var record = Create(DateTime.UtcNow.AddSeconds(offsetSeconds));

        Assert.True(record.HasClockSkew);
    }

    [Fact]
    public void ANullFingerprintIsAllowed()
    {
        // Only Error and Fatal records get one. Information and Warning are kept as context for
        // the evidence window but are not what incidents are raised from.
        var record = Create(DateTime.UtcNow, fingerprint: null);

        Assert.Null(record.Fingerprint);
    }

    [Fact]
    public void ARecordStandsForOneEventUnlessToldOtherwise()
    {
        Assert.Equal(1, Create(DateTime.UtcNow).Occurrences);
    }

    [Fact]
    public void ARecordCannotStandForNoEvents()
    {
        // A zero-weight row would be stored and then vanish from every count — a line in the
        // evidence that detection cannot see.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LogRecord.Create(
                Organization,
                Guid.NewGuid(),
                "event-1",
                "checkout-service",
                LogSeverity.Error,
                "Payment failed",
                "Payment failed",
                null,
                null,
                "abc123",
                DateTime.UtcNow,
                Tolerance,
                occurrences: 0
            )
        );
    }
}
