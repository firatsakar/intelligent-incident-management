using NSubstitute;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Queries.GetSignals;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Tests;

// The query has two shapes. Without a window it is a status queue; with one it is everything
// detected in that span, whatever its status. Getting the second wrong would quietly hide the
// contrast between bursts that were promoted and bursts that were not, which is the only reason
// to look at a span in the first place.
public sealed class GetSignalsQueryHandlerTests
{
    private static readonly DateTime Noon = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly ISignalRepository _signals = Substitute.For<ISignalRepository>();
    private readonly IErrorSignatureRepository _signatures =
        Substitute.For<IErrorSignatureRepository>();
    private readonly GetSignalsQueryHandler _handler;

    public GetSignalsQueryHandlerTests()
    {
        _handler = new GetSignalsQueryHandler(_signals, _signatures);
    }

    private static ErrorSignature Signature(string service = "checkout-service") =>
        ErrorSignature.Create(
            "abc123",
            service,
            "TimeoutException",
            "Payment for order {OrderId} failed",
            Noon
        );

    private static Signal SignalFor(Guid signatureId, SignalStatus status = SignalStatus.Weak)
    {
        var signal = Signal.Detect(
            signatureId,
            SignalKind.LogBurst,
            detectedAt: Noon,
            windowStart: Noon,
            windowEnd: Noon.AddMinutes(5),
            occurrenceCount: 6
        );

        signal.Score(0.65, new Dictionary<string, double> { ["burstBase"] = 0.55 });

        if (status == SignalStatus.Weak)
            signal.MarkWeak("below the threshold");

        return signal;
    }

    private void GivenSignatures(params ErrorSignature[] signatures)
    {
        IReadOnlyList<ErrorSignature> found = signatures;

        _signatures
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(found);
    }

    private void GivenStatusQueryReturns(params Signal[] signals)
    {
        IReadOnlyList<Signal> result = signals;

        _signals
            .GetByStatusAsync(Arg.Any<SignalStatus>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private void GivenWindowQueryReturns(params Signal[] signals) =>
        GivenWindowQueryReturns(signals.Length, signals);

    /// <summary>
    /// The total is stated separately from the page on purpose: the whole point of the envelope
    /// is that a truncated window can say how much it left out.
    /// </summary>
    private void GivenWindowQueryReturns(int totalCount, params Signal[] signals)
    {
        (IReadOnlyList<Signal> Items, int TotalCount) result = (signals, totalCount);

        _signals
            .GetRecentAsync(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(result);
    }

    [Fact]
    public async Task WithoutAWindow_ReadsTheStatusQueue()
    {
        var signature = Signature();
        GivenStatusQueryReturns(SignalFor(signature.Id));
        GivenSignatures(signature);

        await _handler.Handle(new GetSignalsQuery(SignalStatus.Weak, 50), CancellationToken.None);

        await _signals
            .Received(1)
            .GetByStatusAsync(SignalStatus.Weak, 50, Arg.Any<CancellationToken>());
        await _signals
            .DidNotReceive()
            .GetRecentAsync(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(500, 200)]
    public async Task TheLimitIsClamped(int requested, int expected)
    {
        GivenStatusQueryReturns();

        await _handler.Handle(
            new GetSignalsQuery(SignalStatus.Weak, requested),
            CancellationToken.None
        );

        await _signals
            .Received(1)
            .GetByStatusAsync(Arg.Any<SignalStatus>(), expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithAWindow_ReadsTheSpanAndIgnoresTheStatus()
    {
        // The status argument keeps its default, and must not narrow a windowed read.
        var signature = Signature();
        GivenWindowQueryReturns(
            SignalFor(signature.Id, SignalStatus.Weak),
            SignalFor(signature.Id, SignalStatus.Recorded)
        );
        GivenSignatures(signature);

        var result = await _handler.Handle(
            new GetSignalsQuery(SignalStatus.Weak, 50, Noon, Noon.AddHours(1)),
            CancellationToken.None
        );

        await _signals
            .Received(1)
            .GetRecentAsync(
                Noon,
                Noon.AddHours(1),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
        await _signals
            .DidNotReceive()
            .GetByStatusAsync(
                Arg.Any<SignalStatus>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, x => x.Status == SignalStatus.Recorded);
    }

    [Fact]
    public async Task AWindowReportsTheCountItWasCutFrom()
    {
        // The reason the envelope exists. A page of two out of ninety is only honest if the
        // ninety travels with it; otherwise a truncated window is indistinguishable from a quiet
        // one, and quiet is the answer this product is in the business of proving.
        var signature = Signature();
        GivenWindowQueryReturns(90, SignalFor(signature.Id), SignalFor(signature.Id));
        GivenSignatures(signature);

        var result = await _handler.Handle(
            new GetSignalsQuery(SignalStatus.Weak, 2, Noon, Noon.AddHours(1)),
            CancellationToken.None
        );

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(90, result.TotalCount);
    }

    [Fact]
    public async Task AnEmptyWindowStillReportsItsTotal()
    {
        // The early return for "no signals" must not skip the envelope, or an offset past the end
        // would report a total of zero and the screen would lose its place.
        GivenWindowQueryReturns(90);

        var result = await _handler.Handle(
            new GetSignalsQuery(SignalStatus.Weak, 50, Noon, Noon.AddHours(1), Offset: 200),
            CancellationToken.None
        );

        Assert.Empty(result.Items);
        Assert.Equal(90, result.TotalCount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(40, 40)]
    public async Task TheOffsetIsNeverNegative(int requested, int expected)
    {
        GivenWindowQueryReturns();

        await _handler.Handle(
            new GetSignalsQuery(SignalStatus.Weak, 50, Noon, Noon.AddHours(1), requested),
            CancellationToken.None
        );

        // Arg.Is rather than a bare value: NSubstitute refuses to mix matchers with literals
        // across parameters of the same type, and both the limit and the offset are ints.
        await _signals
            .Received(1)
            .GetRecentAsync(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Is(expected),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task TheWindowLimitIsClampedToo()
    {
        // The cap used to apply only to the status queue, which left the windowed path — the one
        // the frontend always takes — as the single read in this service whose cost was set by
        // the customer's error rate.
        GivenWindowQueryReturns();

        await _handler.Handle(
            new GetSignalsQuery(SignalStatus.Weak, 5000, Noon, Noon.AddHours(1)),
            CancellationToken.None
        );

        await _signals
            .Received(1)
            .GetRecentAsync(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Is(GetSignalsQueryHandler.MaxLimit),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task HalfAWindowFallsBackToTheStatusQueue(bool hasFrom, bool hasTo)
    {
        // The controller rejects this, but the handler must not silently read an open-ended span
        // if some other caller gets it wrong.
        GivenStatusQueryReturns();

        await _handler.Handle(
            new GetSignalsQuery(
                SignalStatus.Weak,
                50,
                hasFrom ? Noon : null,
                hasTo ? Noon.AddHours(1) : null
            ),
            CancellationToken.None
        );

        await _signals
            .Received(1)
            .GetByStatusAsync(
                Arg.Any<SignalStatus>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task TheSignalCarriesWhatBrokeAndWhere()
    {
        // Without these a weak-signal row says "0.65, below the threshold" and nothing about what
        // failed — and the heat map has nothing to place.
        var signature = Signature();
        GivenStatusQueryReturns(SignalFor(signature.Id));
        GivenSignatures(signature);

        var signal = Assert.Single(
            (await _handler.Handle(new GetSignalsQuery(), CancellationToken.None)).Items
        );

        Assert.Equal("checkout-service", signal.Service);
        Assert.Equal("TimeoutException", signal.ExceptionType);
        Assert.Equal("Payment for order {OrderId} failed", signal.NormalizedMessage);
        Assert.Equal(0.65, signal.Confidence);
    }

    [Fact]
    public async Task ASignalWhoseSignatureIsGoneIsStillListed()
    {
        // Losing the description is worse than losing the row: the confidence and the reason are
        // still worth showing.
        GivenStatusQueryReturns(SignalFor(Guid.NewGuid()));
        GivenSignatures();

        var signal = Assert.Single(
            (await _handler.Handle(new GetSignalsQuery(), CancellationToken.None)).Items
        );

        Assert.Null(signal.Service);
        Assert.Null(signal.ExceptionType);
        Assert.Equal(0.65, signal.Confidence);
    }

    [Fact]
    public async Task SignaturesAreReadOncePerBatch_NotOncePerRow()
    {
        var signature = Signature();
        GivenStatusQueryReturns(
            SignalFor(signature.Id),
            SignalFor(signature.Id),
            SignalFor(signature.Id)
        );
        GivenSignatures(signature);

        await _handler.Handle(new GetSignalsQuery(), CancellationToken.None);

        await _signatures
            .Received(1)
            .GetByIdsAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NoSignalsMeansNoSignatureRead()
    {
        GivenStatusQueryReturns();

        Assert.Empty(
            (await _handler.Handle(new GetSignalsQuery(), CancellationToken.None)).Items
        );

        await _signatures
            .DidNotReceive()
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }
}
