using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Tests;

// The running state of one distinct error. Dedup, counters and the precedent weight all hang off
// it, so its ageing rule decides between an incident storm and silence.
public sealed class ErrorSignatureTests
{
    // One organisation for the whole file. These are unit tests of rules, not of scoping — the
    // filters that make the column matter live in the DbContext — so the value only has to be
    // consistent.
    private static readonly Guid Organization = Guid.NewGuid();
    private static readonly DateTime Noon = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    private static ErrorSignature Create(DateTime? seenAt = null) =>
        ErrorSignature.Create(
            Organization,
            "abc123",
            "checkout-service",
            "TimeoutException",
            "Payment for order {OrderId} failed",
            seenAt ?? Noon
        );

    [Fact]
    public void Create_CountsTheFirstOccurrence()
    {
        var signature = Create();

        Assert.Equal(1, signature.OccurrenceCount);
        Assert.Equal(Noon, signature.FirstSeenAt);
        Assert.Equal(Noon, signature.LastSeenAt);
        Assert.Null(signature.CurrentIncidentId);
        Assert.Equal(0, signature.PromotionCount);
    }

    public sealed class RecordOccurrence
    {
        [Fact]
        public void AddsToTheCount()
        {
            var signature = Create();

            signature.RecordOccurrence(Noon.AddMinutes(1), count: 29);

            Assert.Equal(30, signature.OccurrenceCount);
        }

        [Fact]
        public void MovesLastSeenForward()
        {
            var signature = Create();

            signature.RecordOccurrence(Noon.AddMinutes(5));

            Assert.Equal(Noon.AddMinutes(5), signature.LastSeenAt);
            Assert.Equal(Noon, signature.FirstSeenAt);
        }

        [Fact]
        public void AnEarlierTimestampMovesFirstSeenBackWithoutTouchingLastSeen()
        {
            // Source clocks deliver out of order and a batch can arrive unsorted. First and last
            // seen have to stay true bounds, because DetectedAt is built from them and correlating
            // evidence against the wrong moment finds nothing.
            var signature = Create();
            signature.RecordOccurrence(Noon.AddMinutes(5));

            signature.RecordOccurrence(Noon.AddMinutes(-10));

            Assert.Equal(Noon.AddMinutes(-10), signature.FirstSeenAt);
            Assert.Equal(Noon.AddMinutes(5), signature.LastSeenAt);
        }

        [Fact]
        public void ACountOfZeroAdjustsTheBoundsWithoutInflatingTheCount()
        {
            // How the poll handler corrects FirstSeenAt after folding a batch.
            var signature = Create();

            signature.RecordOccurrence(Noon.AddMinutes(-10), count: 0);

            Assert.Equal(1, signature.OccurrenceCount);
            Assert.Equal(Noon.AddMinutes(-10), signature.FirstSeenAt);
        }

        [Fact]
        public void DoesNotCountAgainstAnIncidentWhileNoneIsOpen()
        {
            var signature = Create();

            signature.RecordOccurrence(Noon.AddMinutes(1), count: 10);

            Assert.Equal(0, signature.CurrentIncidentOccurrences);
        }

        [Fact]
        public void CountsAgainstTheOpenIncidentWhileOneIsAttached()
        {
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);

            signature.RecordOccurrence(Noon.AddMinutes(1), count: 10);

            Assert.Equal(10, signature.CurrentIncidentOccurrences);
            Assert.Equal(11, signature.OccurrenceCount);
        }
    }

    public sealed class IncidentLifecycle
    {
        [Fact]
        public void AttachIncident_StartsAFreshCounterAndRecordsThePromotion()
        {
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);
            signature.RecordOccurrence(Noon.AddMinutes(1), count: 5);

            var second = Guid.NewGuid();
            signature.AttachIncident(second, Noon.AddHours(30));

            Assert.Equal(second, signature.CurrentIncidentId);
            Assert.Equal(0, signature.CurrentIncidentOccurrences);
            Assert.Equal(2, signature.PromotionCount);
            Assert.Equal(Noon.AddHours(30), signature.LastPromotedAt);
        }

        [Fact]
        public void DetachIncident_AsRealFeedsThePrecedentBonus()
        {
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);

            signature.DetachIncident(wasRealIncident: true);

            Assert.Null(signature.CurrentIncidentId);
            Assert.Equal(1, signature.ConfirmedRealCount);
            Assert.Equal(0, signature.FalsePositiveCount);
        }

        [Fact]
        public void DetachIncident_AsFalsePositiveFeedsThePenalty()
        {
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);

            signature.DetachIncident(wasRealIncident: false);

            Assert.Null(signature.CurrentIncidentId);
            Assert.Equal(0, signature.ConfirmedRealCount);
            Assert.Equal(1, signature.FalsePositiveCount);
        }

        [Fact]
        public void MuteAndUnmute()
        {
            var signature = Create();
            Assert.False(signature.IsMuted);

            signature.Mute();
            Assert.True(signature.IsMuted);

            signature.Unmute();
            Assert.False(signature.IsMuted);
        }
    }

    // The ageing rule, and the reason it exists: an open incident absorbs a new burst only while
    // the signature is still active. Get it wrong one way and every burst opens its own incident;
    // wrong the other way and a burst a week later silently bumps a counter nobody is watching.
    public sealed class CanAbsorbInto
    {
        private static readonly TimeSpan DedupWindow = TimeSpan.FromHours(24);

        [Fact]
        public void IsFalseWhenNoIncidentIsOpen()
        {
            var signature = Create();

            Assert.False(signature.CanAbsorbInto(Noon.AddMinutes(1), DedupWindow));
        }

        [Fact]
        public void IsTrueWhileTheSignatureIsStillActive()
        {
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);

            Assert.True(signature.CanAbsorbInto(Noon.AddHours(1), DedupWindow));
        }

        [Fact]
        public void IsTrueExactlyAtTheWindowEdge()
        {
            // The comparison is inclusive; pinned so a later <= / < edit is a visible change.
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);

            Assert.True(signature.CanAbsorbInto(Noon.Add(DedupWindow), DedupWindow));
        }

        [Fact]
        public void IsFalseOnceTheSignatureHasBeenQuietLongerThanTheWindow()
        {
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);

            Assert.False(
                signature.CanAbsorbInto(Noon.Add(DedupWindow).AddSeconds(1), DedupWindow)
            );
        }

        [Fact]
        public void TheWindowIsMeasuredFromTheLastOccurrence_NotFromThePromotion()
        {
            // A signature that keeps firing keeps its incident absorbing, however long ago the
            // incident was opened. This is what makes "still active" mean activity rather than age.
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);
            signature.RecordOccurrence(Noon.AddHours(40));

            Assert.True(signature.CanAbsorbInto(Noon.AddHours(41), DedupWindow));
        }

        [Fact]
        public void IsFalseAfterTheIncidentIsResolved()
        {
            var signature = Create();
            signature.AttachIncident(Guid.NewGuid(), Noon);
            signature.DetachIncident(wasRealIncident: true);

            Assert.False(signature.CanAbsorbInto(Noon.AddMinutes(1), DedupWindow));
        }
    }
}
