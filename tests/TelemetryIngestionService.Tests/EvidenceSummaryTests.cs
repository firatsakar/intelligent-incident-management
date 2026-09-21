using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Tests;

// The incident description a promoted signal carries. It is the only thing the AI analysis
// receives beyond the title, and it costs no extra call — so what it leaves out, the analysis
// never learns.
public sealed class EvidenceSummaryTests
{
    private static readonly DateTime Noon = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    private static ErrorSignature Signature(string? exceptionType = "System.TimeoutException") =>
        ErrorSignature.Create(
            "abc123",
            "checkout-service",
            exceptionType,
            "Payment for order {OrderId} failed",
            Noon
        );

    private static Signal ScoredSignal(long occurrences = 30, double confidence = 0.90)
    {
        var signal = Signal.Detect(
            Guid.NewGuid(),
            SignalKind.LogBurst,
            detectedAt: Noon,
            windowStart: Noon,
            windowEnd: Noon.AddMinutes(5),
            occurrenceCount: occurrences
        );

        signal.Score(
            confidence,
            new Dictionary<string, double>
            {
                ["burstBase"] = 0.55,
                ["overThreshold"] = 0.10,
                ["rateAnomaly"] = 0.25,
                ["total"] = confidence,
            }
        );

        return signal;
    }

    public sealed class BuildTitle
    {
        [Fact]
        public void UsesTheExceptionTypeWithoutItsNamespace()
        {
            // Namespaces make a poor headline; the type name carries the meaning.
            Assert.Equal(
                "checkout-service: TimeoutException",
                EvidenceSummary.BuildTitle(Signature())
            );
        }

        [Fact]
        public void KeepsAnUnqualifiedExceptionTypeAsItIs()
        {
            Assert.Equal(
                "checkout-service: TimeoutException",
                EvidenceSummary.BuildTitle(Signature("TimeoutException"))
            );
        }

        [Fact]
        public void FallsBackToTheNormalizedMessageWhenNoExceptionWasReported()
        {
            // A logged error without an exception is still an error worth a headline.
            Assert.Equal(
                "checkout-service: Payment for order {OrderId} failed",
                EvidenceSummary.BuildTitle(Signature(exceptionType: null))
            );
        }
    }

    public sealed class Build
    {
        [Fact]
        public void CarriesTheNumbersTheAnalysisIsMeantToQuoteBack()
        {
            var signature = Signature();
            signature.RecordOccurrence(Noon.AddMinutes(4), count: 199);

            var summary = EvidenceSummary.Build(signature, ScoredSignal(), distinctServices: 2, null);

            // How many times in the window, how many in total, how many services, and how
            // confident the deterministic gate was. These are the four the AI's confidence
            // calibration leans on.
            Assert.Contains("30 time(s)", summary);
            Assert.Contains("Occurrences in window: 30", summary);
            Assert.Contains("Total occurrences recorded: 200", summary);
            Assert.Contains("Affected services: 2", summary);
            Assert.Contains("Detection confidence:", summary);
            Assert.Contains("System.TimeoutException", summary);
            Assert.Contains("Payment for order {OrderId} failed", summary);
        }

        [Fact]
        public void ExplainsWhyTheSignalWasRaised()
        {
            var summary = EvidenceSummary.Build(Signature(), ScoredSignal(), 1, null);

            Assert.Contains("Why this was raised", summary);
            Assert.Contains("rateAnomaly", summary);
            Assert.Contains("burstBase", summary);
        }

        [Fact]
        public void OrdersTheBreakdownByWeight()
        {
            // Largest contribution first, so the reason a signal was raised is the first thing
            // read rather than something to be hunted for.
            var summary = EvidenceSummary.Build(Signature(), ScoredSignal(confidence: 0.90), 1, null);

            var reasons = summary[summary.IndexOf("Why this was raised", StringComparison.Ordinal)..];

            Assert.True(
                reasons.IndexOf("burstBase", StringComparison.Ordinal)
                    < reasons.IndexOf("rateAnomaly", StringComparison.Ordinal)
            );
            Assert.True(
                reasons.IndexOf("rateAnomaly", StringComparison.Ordinal)
                    < reasons.IndexOf("overThreshold", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void OmitsPriorHistoryUntilThereIsSome()
        {
            var summary = EvidenceSummary.Build(Signature(), ScoredSignal(), 1, null);

            Assert.DoesNotContain("Prior history", summary);
        }

        [Fact]
        public void IncludesPriorHistoryOnceTheSignatureHasBeenPromotedBefore()
        {
            // The line that tells the analysis this is a repeat offender, and whether the last
            // few calls were right.
            var signature = Signature();
            signature.AttachIncident(Guid.NewGuid(), Noon);
            signature.DetachIncident(wasRealIncident: true);
            signature.AttachIncident(Guid.NewGuid(), Noon.AddHours(30));
            signature.DetachIncident(wasRealIncident: false);

            var summary = EvidenceSummary.Build(signature, ScoredSignal(), 1, null);

            Assert.Contains("promoted 2 time(s)", summary);
            Assert.Contains("1 confirmed real", summary);
            Assert.Contains("1 false positive", summary);
        }

        [Fact]
        public void OmitsTheStackTraceSectionWhenThereIsNoTrace()
        {
            var summary = EvidenceSummary.Build(Signature(), ScoredSignal(), 1, sampleStackTrace: null);

            Assert.DoesNotContain("Representative stack trace", summary);
        }

        [Fact]
        public void IncludesAStackTraceWhenOneWasSampled()
        {
            var summary = EvidenceSummary.Build(
                Signature(),
                ScoredSignal(),
                1,
                "   at Checkout.Pay(Order order)"
            );

            Assert.Contains("Representative stack trace", summary);
            Assert.Contains("at Checkout.Pay(Order order)", summary);
        }

        [Fact]
        public void TruncatesALongStackTrace()
        {
            // The description is carried in an event and read by a model; an unbounded trace
            // would dominate both.
            var summary = EvidenceSummary.Build(
                Signature(),
                ScoredSignal(),
                1,
                new string('z', 5000)
            );

            Assert.Contains("…", summary);
            Assert.DoesNotContain(new string('z', 1201), summary);
        }
    }
}
