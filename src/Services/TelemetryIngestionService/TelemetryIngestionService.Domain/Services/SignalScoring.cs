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
    private const double BlastRadiusBonus = 0.10;
    private const double MutedPenalty = -0.40;

    // The history term's two ends: what an all-real record is worth, and what an all-false one
    // costs. Asymmetric on purpose — waking somebody for nothing again is the worse mistake.
    public const double HistoryCeiling = 0.15;
    public const double HistoryFloor = -0.25;

    // How many verdicts it takes to be believed. With n of them the term carries n/(n+2) of its
    // value: one verdict a third, ten five-sixths. A single closure is an anecdote, not a record.
    private const double HistoryPriorWeight = 2.0;

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

        // What this signature's incidents turned out to be when people closed them. This is the
        // term that makes the system improve with use.
        if (History(inputs.ConfirmedRealCount, inputs.FalsePositiveCount) is { } history)
        {
            confidence += history;
            breakdown["history"] = history;
        }

        if (inputs.DistinctServices >= 2)
        {
            confidence += BlastRadiusBonus;
            breakdown["blastRadius"] = BlastRadiusBonus;
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

    /// <summary>
    /// The signature's record as one bounded number, or null when it has none.
    /// </summary>
    /// <remarks>
    /// It replaced two independent flags — +0.15 if any incident was ever real, −0.25 if any was
    /// ever a false alarm — which looked at whether a count was non-zero and nothing else. Nine
    /// real incidents and one false alarm scored −0.10 under them: a single mistake outweighed
    /// nine confirmations, for ever. This reads the share instead, and discounts it while there
    /// are few verdicts; however many accumulate, it stays within
    /// [<see cref="HistoryFloor"/>, <see cref="HistoryCeiling"/>], so a record corroborates a
    /// burst and can never stand in for one.
    /// </remarks>
    public static double? History(int confirmedReal, int falsePositive)
    {
        var verdicts = confirmedReal + falsePositive;

        if (verdicts == 0)
            return null;

        var realShare = (double)confirmedReal / verdicts;
        var value = HistoryCeiling * realShare + HistoryFloor * (1 - realShare);
        var belief = verdicts / (verdicts + HistoryPriorWeight);

        // Four places: the breakdown is shown and quoted to the model, and 0.091666… is noise.
        return Math.Round(value * belief, 4);
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
