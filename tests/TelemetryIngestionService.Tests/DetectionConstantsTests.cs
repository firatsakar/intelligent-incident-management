using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Tests;

// These are product decisions expressed as constants, not implementation details. Changing one
// silently changes when the platform wakes somebody up, so each is pinned here rather than left
// to be rediscovered from behaviour.
public sealed class DetectionConstantsTests
{
    [Fact]
    public void BurstBase_IsBelowTheWeakBand_SoAThresholdBreachAloneIsNotEnough()
    {
        // 0.55 sits under the 0.60 weak-band floor on purpose: merely clearing the threshold buys
        // a record, not attention. At least one corroborating term has to fire first.
        Assert.Equal(0.55, SignalScoring.BurstBase);
    }

    [Fact]
    public void MinimumSamples_IsFive()
    {
        // Calling something anomalous against fewer data points is worse than saying nothing.
        Assert.Equal(5, RateBaseline.MinimumSamples);
    }

    [Fact]
    public void LogSeverity_OrdersErrorBelowFatal()
    {
        // The ingest path fingerprints on `severity >= LogSeverity.Error`, and the fatal
        // short-circuit keys off Fatal being the top of the scale. Reordering this enum would
        // change what gets fingerprinted without touching a line of detection code.
        Assert.True(LogSeverity.Error > LogSeverity.Warning);
        Assert.True(LogSeverity.Fatal > LogSeverity.Error);
    }
}
