using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Domain.Services;

// Decides how confident we are that a burst is a real incident. Every component is deterministic
// and recorded, because the gate that wakes someone at 3am has to be explainable — and because an
// LLM asked the same question would be slower, dearer and different every time.
public static class SignalScoring
{
    public const double BurstBase = 0.55;

    private const double OverThresholdStep = 0.10;
    private const double OverThresholdCap = 0.30;
    private const double AnomalyBonus = 0.25;
    private const double PrecedentBonus = 0.15;
    private const double BlastRadiusBonus = 0.10;
    private const double MutedPenalty = -0.40;
    private const double FalsePositivePenalty = -0.25;

    // Above this the rate is unusual enough for its own history to count as corroboration.
    private const double AnomalyZScoreThreshold = 2.0;

    public sealed record Inputs
    {
        public required long Occurrences { get; init; }
        public required int Threshold { get; init; }
        public double? ZScore { get; init; }
        public required int DistinctServices { get; init; }
        public required bool IsMuted { get; init; }
        public required int ConfirmedRealCount { get; init; }
        public required int FalsePositiveCount { get; init; }

        // A crash is not a judgement call. It bypasses scoring entirely.
        public required bool IsFatal { get; init; }
    }

    public sealed record Result(double Confidence, IReadOnlyDictionary<string, double> Breakdown);

    public static Result Score(Inputs inputs)
    {
        var breakdown = new Dictionary<string, double>();

        if (inputs.IsFatal)
        {
            breakdown["fatal"] = 1.0;

            return new Result(1.0, breakdown);
        }

        var confidence = BurstBase;
        breakdown["burstBase"] = BurstBase;

        // How far past the threshold, in doublings. A burst twice the threshold is meaningfully
        // worse than one that merely clears it; a burst fifty times over is not five times worse
        // again, hence the cap.
        if (inputs.Threshold > 0 && inputs.Occurrences > inputs.Threshold)
        {
            var doublings = Math.Floor(Math.Log2((double)inputs.Occurrences / inputs.Threshold));
            var bonus = Math.Min(doublings * OverThresholdStep, OverThresholdCap);

            if (bonus > 0)
            {
                confidence += bonus;
                breakdown["overThreshold"] = bonus;
            }
        }

        // The signature's own history says this rate is unusual — the strongest corroboration
        // available without a second data source.
        if (inputs.ZScore is { } zScore && zScore >= AnomalyZScoreThreshold)
        {
            confidence += AnomalyBonus;
            breakdown["rateAnomaly"] = AnomalyBonus;
        }

        // This signature has produced a real incident before. This is the term that makes the
        // system improve with use.
        if (inputs.ConfirmedRealCount > 0)
        {
            confidence += PrecedentBonus;
            breakdown["precedent"] = PrecedentBonus;
        }

        if (inputs.DistinctServices >= 2)
        {
            confidence += BlastRadiusBonus;
            breakdown["blastRadius"] = BlastRadiusBonus;
        }

        if (inputs.FalsePositiveCount > 0)
        {
            confidence += FalsePositivePenalty;
            breakdown["falsePositivePrecedent"] = FalsePositivePenalty;
        }

        if (inputs.IsMuted)
        {
            confidence += MutedPenalty;
            breakdown["muted"] = MutedPenalty;
        }

        confidence = Math.Clamp(confidence, 0d, 1d);
        breakdown["total"] = confidence;

        return new Result(confidence, breakdown);
    }

    // Severity is only a starting point — the AI analysis that follows sets the real priority.
    public static string SuggestSeverity(Inputs inputs)
    {
        if (inputs.IsFatal)
            return "Critical";

        if (inputs.Threshold > 0 && inputs.Occurrences >= inputs.Threshold * 10)
            return "High";

        return "Medium";
    }

    public static bool ShouldPromote(double confidence, DetectionRule rule) =>
        confidence >= rule.PromoteThreshold;

    // The band below promotion where a signal is worth showing a human but not worth waking one.
    public static bool IsWeak(double confidence, DetectionRule rule) =>
        confidence >= 0.60 && confidence < rule.PromoteThreshold;
}
