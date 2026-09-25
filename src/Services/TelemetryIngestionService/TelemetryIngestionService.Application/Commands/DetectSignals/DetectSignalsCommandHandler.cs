using BuildingBlocks.SharedKernel;
using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
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
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<DetectSignalsCommandHandler> _logger;
    private readonly IOrganizationContext _organization;

    public DetectSignalsCommandHandler(
        IErrorSignatureRepository signatures,
        IDetectionRuleRepository rules,
        ILogRecordRepository logRecords,
        ISignalRepository signals,
        IRealtimeNotifier realtime,
        ILogger<DetectSignalsCommandHandler> logger,
        IOrganizationContext organization
    )
    {
        _signatures = signatures;
        _rules = rules;
        _logRecords = logRecords;
        _signals = signals;
        _realtime = realtime;
        _logger = logger;
        _organization = organization;
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

        var detected = new List<(Signal Signal, ErrorSignature Signature)>();

        foreach (var signature in signatures)
        {
            if (await TryDetectAsync(signature, rules, cancellationToken) is { } signal)
                detected.Add((signal, signature));
        }

        // One SaveChanges for the batch: the interceptor harvests any promotion events into the
        // outbox in the same transaction, so a promotion and its message commit together or not
        // at all.
        if (detected.Count > 0)
            await _signals.SaveChangesAsync(cancellationToken);

        // Announced only once committed, and with the signature attached — a signal that cannot
        // say which service broke has nowhere to land on the heat map.
        //
        // The signature goes out too. It changed in the same transaction — it may have gained an
        // incident, its occurrence and promotion counters moved — and broadcasting only the
        // signal left the evidence screen's signature column going stale while signals were
        // still arriving live on the very same push.
        foreach (var (signal, signature) in detected)
        {
            await _realtime.SignalRecordedAsync(
                SignalDto.FromDomain(signal, signature),
                cancellationToken
            );

            await _realtime.SignatureChangedAsync(
                ErrorSignatureDto.FromDomain(signature),
                cancellationToken
            );
        }

        return detected.Count;
    }

    // Returns the signal it raised, or null when this signature did not warrant one.
    private async Task<Signal?> TryDetectAsync(
        ErrorSignature signature,
        IReadOnlyList<DetectionRule> rules,
        CancellationToken cancellationToken
    )
    {
        var rule = ResolveRule(signature, rules);

        if (rule is null)
            return null;

        var now = DateTime.UtcNow;
        var windowStart = now - rule.Window;

        var occurrences = await _logRecords.CountByFingerprintAsync(
            signature.Fingerprint,
            windowStart,
            now,
            cancellationToken
        );

        // A crash has to bypass the threshold, not merely the scoring: waiting for three of them
        // before looking would defeat the point of treating one as conclusive.
        var hasFatal = await _logRecords.HasFatalAsync(
            signature.Fingerprint,
            windowStart,
            now,
            cancellationToken
        );

        if (!hasFatal && occurrences < rule.Threshold)
            return null;

        // A signature that keeps firing must not raise a fresh signal every poll. One signal per
        // window is what makes the count meaningful rather than a measure of how often we looked.
        var latest = await _signals.GetLatestForSignatureAsync(signature.Id, cancellationToken);

        if (latest is not null && latest.WindowEnd >= windowStart)
        {
            _logger.LogDebug(
                "Signature {Fingerprint} already has a signal covering this window; skipping.",
                signature.Fingerprint
            );

            return null;
        }

        var signal = Signal.Detect(
            // The scope the polling loop took from the source's row, carried down through
            // the poll and the detection into the signal it produces.
            _organization.Required,
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

        var distinctServices = await _logRecords.CountDistinctServicesAsync(
            signature.Fingerprint,
            windowStart,
            now,
            cancellationToken
        );

        var inputs = new SignalScoring.Inputs
        {
            Occurrences = occurrences,
            Threshold = rule.Threshold,
            ZScore = zScore,
            DistinctServices = distinctServices,
            IsMuted = signature.IsMuted,
            ConfirmedRealCount = signature.ConfirmedRealCount,
            FalsePositiveCount = signature.FalsePositiveCount,
            IsFatal = hasFatal,
        };

        var score = SignalScoring.Score(inputs);

        signal.Score(score.Confidence, score.Breakdown);

        _logger.LogInformation(
            "Burst detected for {Service} / {ExceptionType}: {Occurrences} occurrence(s) in {WindowSeconds}s (threshold {Threshold}, z-score {ZScore}, confidence {Confidence:F2}).",
            signature.Service,
            signature.ExceptionType ?? "none",
            occurrences,
            rule.WindowSeconds,
            rule.Threshold,
            zScore?.ToString("F2") ?? "n/a",
            score.Confidence
        );

        await ResolveAsync(signal, signature, rule, inputs, distinctServices, cancellationToken);

        return signal;
    }

    // What happens to a scored signal: promote, absorb into the incident already open for this
    // signature, hold as a weak signal, or just record it.
    private async Task ResolveAsync(
        Signal signal,
        ErrorSignature signature,
        DetectionRule rule,
        SignalScoring.Inputs inputs,
        int distinctServices,
        CancellationToken cancellationToken
    )
    {
        if (signature.IsMuted)
        {
            signal.MarkSuppressed("The signature is muted.");
            return;
        }

        if (!SignalScoring.ShouldPromote(signal.Confidence, rule))
        {
            if (SignalScoring.IsWeak(signal.Confidence, rule))
                signal.MarkWeak(Explain(signal.Confidence, rule.PromoteThreshold, "is below"));

            return;
        }

        // The ageing rule. An open incident absorbs a new burst only while the signature is still
        // active; once it has been quiet longer than the dedup window, the next burst deserves an
        // incident of its own rather than bumping a stale counter.
        if (signature.CanAbsorbInto(DateTime.UtcNow, rule.DedupWindow))
        {
            signal.MarkDeduplicated(
                signature.CurrentIncidentId!.Value,
                $"Incident {signature.CurrentIncidentId} is already open for this signature."
            );

            _logger.LogInformation(
                "Signature {Fingerprint} already has incident {IncidentId} open; counting into it instead of opening another.",
                signature.Fingerprint,
                signature.CurrentIncidentId
            );

            return;
        }

        var sampleStackTrace = await _logRecords.GetSampleStackTraceAsync(
            signature.Fingerprint,
            signal.WindowStart,
            signal.WindowEnd,
            cancellationToken
        );

        var incidentId = Guid.NewGuid();

        signal.Promote(
            incidentId,
            EvidenceSummary.BuildTitle(signature),
            EvidenceSummary.Build(signature, signal, distinctServices, sampleStackTrace),
            SignalScoring.SuggestSeverity(inputs),
            signature.Service,
            signature.Fingerprint,
            Explain(signal.Confidence, rule.PromoteThreshold, "met")
        );

        signature.AttachIncident(incidentId, DateTime.UtcNow);

        _logger.LogInformation(
            "Promoted signature {Fingerprint} to incident {IncidentId} at confidence {Confidence:F2}.",
            signature.Fingerprint,
            incidentId,
            signal.Confidence
        );
    }

    private async Task<double?> ComputeZScoreAsync(
        ErrorSignature signature,
        DetectionRule rule,
        DateTime windowStart,
        CancellationToken cancellationToken
    )
    {
        var baselineStart = windowStart - (rule.Window * BaselineWindows);

        var timestamps = await _logRecords.GetOccurrencesByFingerprintAsync(
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

    // This reason is stored on the signal and read back by a person, so it is formatted
    // invariantly rather than in whatever culture the service happens to be started under.
    // EvidenceSummary already does this deliberately; a reason that reads "1,00" on one machine
    // and "1.00" on another is the same bug in a second place.
    private static string Explain(double confidence, double threshold, string verb) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Confidence {confidence:F2} {verb} the promotion threshold of {threshold:F2}."
        );

    private static DateTime MaxOf(DateTime left, DateTime right) => left > right ? left : right;
}
