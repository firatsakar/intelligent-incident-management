using MediatR;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Application.Commands.DetectSignals;

public sealed class DetectSignalsCommandHandler : IRequestHandler<DetectSignalsCommand, int>
{
    // How many complete windows of history the baseline is built from.
    private const int BaselineWindows = 12;

    private readonly IErrorSignatureRepository _signatures;
    private readonly IDetectionRuleRepository _rules;
    private readonly ILogRecordRepository _logRecords;
    private readonly ISignalRepository _signals;
    private readonly ILogger<DetectSignalsCommandHandler> _logger;

    public DetectSignalsCommandHandler(
        IErrorSignatureRepository signatures,
        IDetectionRuleRepository rules,
        ILogRecordRepository logRecords,
        ISignalRepository signals,
        ILogger<DetectSignalsCommandHandler> logger
    )
    {
        _signatures = signatures;
        _rules = rules;
        _logRecords = logRecords;
        _signals = signals;
        _logger = logger;
    }

    public async Task<int> Handle(
        DetectSignalsCommand request,
        CancellationToken cancellationToken
    )
    {
        if (request.Fingerprints.Count == 0)
            return 0;

        var rules = await _rules.GetEnabledAsync(cancellationToken);

        if (rules.Count == 0)
        {
            _logger.LogWarning("No enabled detection rules; nothing can be detected.");
            return 0;
        }

        var signatures = await _signatures.GetByFingerprintsAsync(
            request.Fingerprints.ToList(),
            cancellationToken
        );

        var detected = 0;

        foreach (var signature in signatures)
        {
            if (await TryDetectAsync(signature, rules, cancellationToken))
                detected++;
        }

        if (detected > 0)
            await _signals.SaveChangesAsync(cancellationToken);

        return detected;
    }

    private async Task<bool> TryDetectAsync(
        ErrorSignature signature,
        IReadOnlyList<DetectionRule> rules,
        CancellationToken cancellationToken
    )
    {
        var rule = ResolveRule(signature, rules);

        if (rule is null)
            return false;

        var now = DateTime.UtcNow;
        var windowStart = now - rule.Window;

        var occurrences = await _logRecords.CountByFingerprintAsync(
            signature.Fingerprint,
            windowStart,
            now,
            cancellationToken
        );

        if (occurrences < rule.Threshold)
            return false;

        // A signature that keeps firing must not raise a fresh signal every poll. One signal per
        // window is what makes the count meaningful rather than a measure of how often we looked.
        var latest = await _signals.GetLatestForSignatureAsync(signature.Id, cancellationToken);

        if (latest is not null && latest.WindowEnd >= windowStart)
        {
            _logger.LogDebug(
                "Signature {Fingerprint} already has a signal covering this window; skipping.",
                signature.Fingerprint
            );

            return false;
        }

        var signal = Signal.Detect(
            signature.Id,
            SignalKind.LogBurst,
            // When the problem started, not when we noticed it.
            detectedAt: signature.LastSeenAt > windowStart ? MaxOf(signature.FirstSeenAt, windowStart) : signature.FirstSeenAt,
            windowStart: windowStart,
            windowEnd: now,
            occurrenceCount: occurrences
        );

        await _signals.AddAsync(signal, cancellationToken);

        var zScore = await ComputeZScoreAsync(signature, rule, windowStart, cancellationToken);

        _logger.LogInformation(
            "Burst detected for {Service} / {ExceptionType}: {Occurrences} occurrence(s) in {WindowSeconds}s (threshold {Threshold}, z-score {ZScore}).",
            signature.Service,
            signature.ExceptionType ?? "none",
            occurrences,
            rule.WindowSeconds,
            rule.Threshold,
            zScore?.ToString("F2") ?? "n/a"
        );

        // Scoring and promotion are IIM-21's job; the z-score is computed here because this is
        // where the window is known, and carried on the signal for it to use.
        signal.Score(
            confidence: 0d,
            breakdown: new Dictionary<string, double>
            {
                ["occurrences"] = occurrences,
                ["threshold"] = rule.Threshold,
                ["zScore"] = zScore ?? double.NaN,
            }
        );

        return true;
    }

    private async Task<double?> ComputeZScoreAsync(
        ErrorSignature signature,
        DetectionRule rule,
        DateTime windowStart,
        CancellationToken cancellationToken
    )
    {
        var baselineStart = windowStart - (rule.Window * BaselineWindows);

        var timestamps = await _logRecords.GetTimestampsByFingerprintAsync(
            signature.Fingerprint,
            baselineStart,
            windowStart,
            cancellationToken
        );

        var buckets = RateBaseline.BucketCounts(
            timestamps,
            baselineStart,
            windowStart,
            rule.Window
        );

        var current = await _logRecords.CountByFingerprintAsync(
            signature.Fingerprint,
            windowStart,
            DateTime.UtcNow,
            cancellationToken
        );

        return RateBaseline.ZScore(buckets, current);
    }

    // A rule naming the service wins over the catch-all, so a noisy service can be tuned without
    // loosening everything else.
    private static DetectionRule? ResolveRule(
        ErrorSignature signature,
        IReadOnlyList<DetectionRule> rules
    )
    {
        return rules
            .Where(rule => rule.AppliesTo(signature.Service))
            .OrderByDescending(rule => rule.Service is not null)
            .FirstOrDefault();
    }

    private static DateTime MaxOf(DateTime left, DateTime right) => left > right ? left : right;
}
