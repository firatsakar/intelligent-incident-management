using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.DetectSignals;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Events;

namespace TelemetryIngestionService.Tests;

// The decision tree between "some errors happened" and "wake somebody up". Every branch of it is
// reachable from production data, and three of them were only ever confirmed by watching a demo
// run by hand.
public sealed class DetectSignalsCommandHandlerTests
{
    private const string Fingerprint = "abc123";
    private const string Service = "checkout-service";

    private const int RuleWindowSeconds = 300;

    // Mirrors DetectSignalsCommandHandler.BaselineWindows, which is private.
    private const int BaselineWindows = 12;

    private readonly IErrorSignatureRepository _signatures =
        Substitute.For<IErrorSignatureRepository>();
    private readonly IDetectionRuleRepository _rules = Substitute.For<IDetectionRuleRepository>();
    private readonly ILogRecordRepository _logRecords = Substitute.For<ILogRecordRepository>();
    private readonly ISignalRepository _signals = Substitute.For<ISignalRepository>();
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();

    private readonly List<Signal> _recorded = [];
    private readonly DetectSignalsCommandHandler _handler;

    public DetectSignalsCommandHandlerTests()
    {
        _signals
            .When(repository =>
                repository.AddAsync(Arg.Any<Signal>(), Arg.Any<CancellationToken>())
            )
            .Do(call => _recorded.Add(call.Arg<Signal>()));

        // No history by default, which is the honest state of a signature firing for the first
        // time — and, as AFlatBaselineHandsEveryBurstTheAnomalyBonus shows, not a neutral one.
        GivenBaseline();

        _handler = new DetectSignalsCommandHandler(
            _signatures,
            _rules,
            _logRecords,
            _signals,
            _realtime,
            NullLogger<DetectSignalsCommandHandler>.Instance
        );
    }

    // ---- arrangement helpers -------------------------------------------------------------

    private static DetectionRule Rule(
        string? service = null,
        int threshold = 3,
        double promoteThreshold = 0.90,
        int dedupWindowHours = 24
    ) =>
        DetectionRule.Create(
            service is null ? "catch-all" : $"rule for {service}",
            service,
            LogSeverity.Error,
            windowSeconds: RuleWindowSeconds,
            threshold: threshold,
            dedupWindowHours: dedupWindowHours,
            promoteThreshold: promoteThreshold
        );

    private void GivenRules(params DetectionRule[] rules)
    {
        IReadOnlyList<DetectionRule> enabled = rules;

        _rules.GetEnabledAsync(Arg.Any<CancellationToken>()).Returns(enabled);
    }

    private ErrorSignature GivenSignature(DateTime? lastSeenAt = null)
    {
        var signature = ErrorSignature.Create(
            Fingerprint,
            Service,
            "TimeoutException",
            "Payment for order {OrderId} failed",
            lastSeenAt ?? DateTime.UtcNow
        );

        IReadOnlyList<ErrorSignature> found = [signature];

        _signatures
            .GetByFingerprintsAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(found);

        return signature;
    }

    // The handler builds its baseline from twelve complete windows before the current one. Each
    // count here fills one of those windows, placed mid-bucket so a few milliseconds of drift
    // between the test's clock and the handler's cannot move a timestamp across a boundary.
    private void GivenBaseline(params int[] countsPerWindow)
    {
        var now = DateTime.UtcNow;
        var window = TimeSpan.FromSeconds(RuleWindowSeconds);
        var baselineStart = now - window - (window * BaselineWindows);

        var timestamps = new List<DateTime>();

        for (var bucket = 0; bucket < countsPerWindow.Length; bucket++)
        {
            var middle = baselineStart + (window * bucket) + (window / 2);

            for (var i = 0; i < countsPerWindow[bucket]; i++)
                timestamps.Add(middle);
        }

        IReadOnlyList<DateTime> history = timestamps;

        _logRecords
            .GetTimestampsByFingerprintAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(history);
    }

    // A baseline that is neither flat nor empty, so the rate anomaly stays out of the way and a
    // test can isolate the term it is actually about. Mean six, standard deviation about 0.8.
    private void GivenOrdinaryBaselineOf(int mean) =>
        GivenBaseline(
            [
                mean,
                mean - 1,
                mean + 1,
                mean,
                mean - 1,
                mean + 1,
                mean,
                mean - 1,
                mean + 1,
                mean,
                mean - 1,
                mean + 1,
            ]
        );

    private void GivenWindow(long occurrences, bool hasFatal = false, int distinctServices = 1)
    {
        _logRecords
            .CountByFingerprintAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(occurrences);

        _logRecords
            .HasFatalAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(hasFatal);

        _logRecords
            .CountDistinctServicesAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(distinctServices);
    }

    private Task<int> Detect() =>
        _handler.Handle(new DetectSignalsCommand([Fingerprint]), CancellationToken.None);

    // ---- the gate ------------------------------------------------------------------------

    [Fact]
    public async Task NoFingerprints_ShortCircuitsBeforeTouchingAnything()
    {
        var detected = await _handler.Handle(new DetectSignalsCommand([]), CancellationToken.None);

        Assert.Equal(0, detected);
        await _rules.DidNotReceive().GetEnabledAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoEnabledRules_DetectsNothing()
    {
        // Nothing can be detected without a rule, and the poll loop must not fall over because of
        // it — a disabled rule set is a configuration state, not a fault.
        GivenRules();
        GivenWindow(occurrences: 500);

        Assert.Equal(0, await Detect());
        Assert.Empty(_recorded);
    }

    [Fact]
    public async Task BelowTheThreshold_RaisesNoSignal()
    {
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 2);

        Assert.Equal(0, await Detect());
        Assert.Empty(_recorded);
    }

    [Fact]
    public async Task AtTheThreshold_RaisesASignal()
    {
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 3);

        Assert.Equal(1, await Detect());
        Assert.Single(_recorded);
        Assert.Equal(3, _recorded[0].OccurrenceCount);
    }

    [Fact]
    public async Task ASingleFatal_BypassesTheThresholdEntirely()
    {
        // The bug caught by hand in Adım 13: the fatal shortcut sat *after* the threshold check,
        // so one crash was filtered out before anything looked at it. Waiting for three crashes
        // before treating one as conclusive defeats the point.
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 1, hasFatal: true);

        Assert.Equal(1, await Detect());
        Assert.Equal(1.0, _recorded[0].Confidence);
        Assert.Equal(SignalStatus.Promoted, _recorded[0].Status);
    }

    // ---- one signal per window, not one per poll -----------------------------------------

    [Fact]
    public async Task ASignalAlreadyCoveringThisWindow_IsNotRaisedAgain()
    {
        // A signature that keeps firing is polled repeatedly. Without this the occurrence count
        // would measure how often we looked rather than how often it broke.
        GivenRules(Rule(threshold: 3));
        var signature = GivenSignature();
        GivenWindow(occurrences: 30);

        _signals
            .GetLatestForSignatureAsync(signature.Id, Arg.Any<CancellationToken>())
            .Returns(
                Signal.Detect(
                    signature.Id,
                    SignalKind.LogBurst,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddMinutes(-2),
                    DateTime.UtcNow.AddMinutes(-1),
                    30
                )
            );

        Assert.Equal(0, await Detect());
        Assert.Empty(_recorded);
    }

    [Fact]
    public async Task ASignalFromAnEarlierWindow_DoesNotBlockANewOne()
    {
        GivenRules(Rule(threshold: 3));
        var signature = GivenSignature();
        GivenWindow(occurrences: 30);

        _signals
            .GetLatestForSignatureAsync(signature.Id, Arg.Any<CancellationToken>())
            .Returns(
                Signal.Detect(
                    signature.Id,
                    SignalKind.LogBurst,
                    DateTime.UtcNow.AddHours(-2),
                    DateTime.UtcNow.AddHours(-2),
                    DateTime.UtcNow.AddHours(-2).AddMinutes(5),
                    30
                )
            );

        Assert.Equal(1, await Detect());
        Assert.Single(_recorded);
    }

    // ---- what happens to a scored signal --------------------------------------------------

    [Fact]
    public async Task AMutedSignature_IsSuppressedWithoutPromotion()
    {
        GivenRules(Rule(threshold: 3));
        var signature = GivenSignature();
        signature.Mute();
        GivenWindow(occurrences: 3000, hasFatal: true);

        await Detect();

        // Recorded, not discarded: a muted signature still feeds the baseline and precedent.
        Assert.Equal(SignalStatus.Suppressed, _recorded[0].Status);
        Assert.Null(_recorded[0].IncidentId);
        Assert.Null(signature.CurrentIncidentId);
    }

    [Fact]
    public async Task AConfidenceInsideTheWeakBand_IsMarkedWeakRatherThanPromoted()
    {
        // Twice the threshold with nothing corroborating it scores 0.65 — worth showing a human,
        // not worth waking one. The baseline has to be an ordinary one for this: see the flat
        // baseline test below for what happens without it.
        GivenRules(Rule(threshold: 3, promoteThreshold: 0.90));
        GivenSignature();
        GivenOrdinaryBaselineOf(mean: 6);
        GivenWindow(occurrences: 6);

        await Detect();

        Assert.Equal(0.65, _recorded[0].Confidence, 10);
        Assert.Equal(SignalStatus.Weak, _recorded[0].Status);
        Assert.NotNull(_recorded[0].Reason);
    }

    [Fact]
    public async Task AFlatBaselineHandsEveryBurstTheAnomalyBonus()
    {
        // Why the weak band was never observed in any manual run, pinned rather than left as
        // folklore. A signature with no history buckets to twelve zeroes, a rise above a flat
        // baseline scores the bounded 4.0, and the same burst that scores 0.65 against an
        // ordinary baseline reaches 0.90 and is promoted.
        //
        // This is working as designed — the first burst from a silent signature really is
        // notable — but it means the band is unreachable until a signature has history, which is
        // worth knowing before tuning anything.
        GivenRules(Rule(threshold: 3, promoteThreshold: 0.90));
        GivenSignature();
        GivenBaseline();
        GivenWindow(occurrences: 6);

        await Detect();

        Assert.Equal(0.90, _recorded[0].Confidence, 10);
        Assert.Equal(SignalStatus.Promoted, _recorded[0].Status);
    }

    [Fact]
    public async Task AConfidenceBelowTheWeakBand_IsRecordedOnly()
    {
        // Below 0.60 nothing is shown and nothing is raised, but the signal is still written —
        // it is what the baseline and the precedent weight learn from.
        GivenRules(Rule(threshold: 3));
        var signature = GivenSignature();
        signature.AttachIncident(Guid.NewGuid(), DateTime.UtcNow);
        signature.DetachIncident(wasRealIncident: false);
        GivenOrdinaryBaselineOf(mean: 3);
        GivenWindow(occurrences: 3);

        await Detect();

        Assert.Equal(0.30, _recorded[0].Confidence, 10);
        Assert.Equal(SignalStatus.Recorded, _recorded[0].Status);
    }

    [Fact]
    public async Task ReachingThePromotionThreshold_OpensAnIncidentAndAttachesIt()
    {
        GivenRules(Rule(threshold: 3));
        var signature = GivenSignature();
        GivenWindow(occurrences: 30, hasFatal: true);

        await Detect();

        var signal = _recorded[0];

        Assert.Equal(SignalStatus.Promoted, signal.Status);
        Assert.NotNull(signal.IncidentId);

        // The id is chosen here, not by IncidentService: at-least-once delivery means the
        // promotion event can arrive twice, and a caller-assigned key turns the second arrival
        // into a duplicate key rather than a second incident.
        Assert.Equal(signal.IncidentId, signature.CurrentIncidentId);
        Assert.Equal(1, signature.PromotionCount);
    }

    [Fact]
    public async Task PromotionRaisesTheDomainEventTheOutboxHarvests()
    {
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 30, hasFatal: true);

        await Detect();

        var promoted = Assert.Single(
            _recorded[0].DomainEvents.OfType<SignalPromotedDomainEvent>()
        );

        Assert.Equal(_recorded[0].IncidentId, promoted.IncidentId);
        Assert.Equal(Service, promoted.Service);
        Assert.Equal(Fingerprint, promoted.Fingerprint);
        Assert.Contains("TimeoutException", promoted.Title);
        Assert.Contains("Evidence", promoted.Description);
    }

    // ---- the ageing rule ------------------------------------------------------------------

    [Fact]
    public async Task AnOpenIncidentInsideTheDedupWindow_AbsorbsTheBurst()
    {
        GivenRules(Rule(threshold: 3, dedupWindowHours: 24));
        var signature = GivenSignature();
        var incidentId = Guid.NewGuid();
        signature.AttachIncident(incidentId, DateTime.UtcNow.AddHours(-1));
        GivenWindow(occurrences: 30, hasFatal: true);

        await Detect();

        Assert.Equal(SignalStatus.Deduplicated, _recorded[0].Status);
        Assert.Equal(incidentId, _recorded[0].IncidentId);

        // Still the same incident — no second promotion.
        Assert.Equal(incidentId, signature.CurrentIncidentId);
        Assert.Equal(1, signature.PromotionCount);
    }

    [Fact]
    public async Task AStaleIncident_DoesNotAbsorbAndANewOneIsOpened()
    {
        // Once the signature has been quiet longer than the dedup window, the next burst deserves
        // its own incident rather than bumping a counter on something nobody is watching.
        GivenRules(Rule(threshold: 3, dedupWindowHours: 24));
        var signature = GivenSignature(lastSeenAt: DateTime.UtcNow.AddHours(-48));
        var stale = Guid.NewGuid();
        signature.AttachIncident(stale, DateTime.UtcNow.AddHours(-48));
        GivenWindow(occurrences: 30, hasFatal: true);

        await Detect();

        Assert.Equal(SignalStatus.Promoted, _recorded[0].Status);
        Assert.NotEqual(stale, _recorded[0].IncidentId);
        Assert.Equal(_recorded[0].IncidentId, signature.CurrentIncidentId);
        Assert.Equal(2, signature.PromotionCount);
    }

    // ---- rule resolution -------------------------------------------------------------------

    [Fact]
    public async Task AServiceSpecificRuleWinsOverTheCatchAll()
    {
        // So a noisy service can be tuned without loosening everything else. Catch-all first in
        // the list on purpose: the ordering must come from the rule, not from the query.
        GivenRules(Rule(threshold: 3), Rule(service: Service, threshold: 100));
        GivenSignature();
        GivenWindow(occurrences: 50);

        Assert.Equal(0, await Detect());
        Assert.Empty(_recorded);
    }

    [Fact]
    public async Task ARuleForAnotherServiceIsIgnored()
    {
        GivenRules(Rule(service: "billing-service", threshold: 100));
        GivenSignature();
        GivenWindow(occurrences: 50);

        Assert.Equal(0, await Detect());
    }

    // ---- persistence ------------------------------------------------------------------------

    [Fact]
    public async Task TheBatchIsSavedOnceSoAPromotionAndItsMessageCommitTogether()
    {
        // One SaveChanges for the batch: the interceptor harvests the promotion event into the
        // outbox in the same transaction, so the incident and its message commit together or not
        // at all.
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 30, hasFatal: true);

        await Detect();

        await _signals.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NothingDetected_MeansNothingSaved()
    {
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 1);

        await Detect();

        await _signals.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- realtime ------------------------------------------------------------------------------

    [Fact]
    public async Task ADetectedSignalIsAnnouncedWithItsSignatureAttached()
    {
        // The signal alone knows only a signature id. Without the service and the error on it,
        // the heat map has nowhere to place the row and a queue entry cannot say what broke.
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 30, hasFatal: true);

        await Detect();

        await _realtime
            .Received(1)
            .SignalRecordedAsync(
                Arg.Is<SignalDto>(dto =>
                    dto.Service == Service
                    && dto.ExceptionType == "TimeoutException"
                    && dto.Status == SignalStatus.Promoted
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task WeakAndSuppressedSignalsAreAnnouncedToo()
    {
        // Not only the promoted ones: the queue and the map exist precisely to show what the gate
        // decided against.
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenOrdinaryBaselineOf(mean: 6);
        GivenWindow(occurrences: 6);

        await Detect();

        await _realtime
            .Received(1)
            .SignalRecordedAsync(
                Arg.Is<SignalDto>(dto => dto.Status == SignalStatus.Weak),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task TheAnnouncementComesAfterTheBatchIsSaved()
    {
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 30, hasFatal: true);

        await Detect();

        Received.InOrder(() =>
        {
            _signals.SaveChangesAsync(Arg.Any<CancellationToken>());
            _realtime.SignalRecordedAsync(Arg.Any<SignalDto>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task TheReasonIsFormattedInvariantlyWhateverCultureTheServiceRunsUnder()
    {
        // Found by reading it on screen: under a Turkish culture the stored reason said
        // "Confidence 1,00 met the promotion threshold of 0,90", so the same signal explained
        // itself differently depending on which machine detected it. EvidenceSummary already
        // formats invariantly on purpose; this is the same rule in the other place it matters.
        var current = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

        try
        {
            GivenRules(Rule(threshold: 3, promoteThreshold: 0.90));
            GivenSignature();
            GivenOrdinaryBaselineOf(mean: 6);
            GivenWindow(occurrences: 6);

            await Detect();

            Assert.Contains("0.65", _recorded[0].Reason);
            Assert.Contains("0.90", _recorded[0].Reason);
            Assert.DoesNotContain(",", _recorded[0].Reason);
        }
        finally
        {
            CultureInfo.CurrentCulture = current;
        }
    }

    [Fact]
    public async Task NothingDetected_MeansNothingAnnounced()
    {
        GivenRules(Rule(threshold: 3));
        GivenSignature();
        GivenWindow(occurrences: 1);

        await Detect();

        await _realtime
            .DidNotReceive()
            .SignalRecordedAsync(Arg.Any<SignalDto>(), Arg.Any<CancellationToken>());
    }
}
