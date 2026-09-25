using NSubstitute;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Queries.GetTelemetryStats;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Tests;

// The funnel is the product's argument in numbers, so the counts have to mean exactly what the
// screen says they mean. The one that matters most is "not raised": count it wrong and the
// product claims restraint it never showed.
public sealed class GetTelemetryStatsQueryHandlerTests
{
    // One organisation for the whole file. These are unit tests of rules, not of scoping — the
    // filters that make the column matter live in the DbContext — so the value only has to be
    // consistent.
    private static readonly Guid Organization = Guid.NewGuid();
    private static readonly DateTime Noon = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly ILogRecordRepository _logRecords = Substitute.For<ILogRecordRepository>();
    private readonly ISignalRepository _signals = Substitute.For<ISignalRepository>();
    private readonly IErrorSignatureRepository _signatures =
        Substitute.For<IErrorSignatureRepository>();
    private readonly GetTelemetryStatsQueryHandler _handler;

    public GetTelemetryStatsQueryHandlerTests()
    {
        _handler = new GetTelemetryStatsQueryHandler(_logRecords, _signals, _signatures);

        GivenLogs(new LogWindowSummary(0, 0, new Dictionary<string, int>(), new Dictionary<LogSeverity, int>()));
        GivenSignalRows();
        GivenSignatures();
    }

    private void GivenLogs(LogWindowSummary summary) =>
        _logRecords
            .GetWindowSummaryAsync(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(summary);

    private void GivenSignalRows(params SignalStatsRow[] rows)
    {
        IReadOnlyList<SignalStatsRow> result = rows;

        _signals
            .GetWindowStatsRowsAsync(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(result);
    }

    private void GivenSignatures(params ErrorSignature[] signatures)
    {
        IReadOnlyList<ErrorSignature> found = signatures;

        _signatures
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(found);
    }

    private static ErrorSignature Signature(
        string service = "checkout-service",
        string? exceptionType = "TimeoutException"
    ) =>
        ErrorSignature.Create(
            Organization,
            Guid.NewGuid().ToString("N"),
            service,
            exceptionType,
            "Payment for order {OrderId} failed",
            Noon
        );

    private static SignalStatsRow Row(
        Guid signatureId,
        SignalStatus status = SignalStatus.Weak,
        Guid? incidentId = null,
        long occurrences = 6
    ) => new(signatureId, status, Noon, incidentId, occurrences);

    [Fact]
    public async Task NotRaisedExcludesDeduplicated()
    {
        // A deduplicated signal was folded into an incident that is already open, so somebody was
        // woken — just earlier. Counting it as restraint would let the product take credit for
        // silence it did not keep.
        var signature = Signature();
        GivenSignatures(signature);
        GivenSignalRows(
            Row(signature.Id, SignalStatus.Weak),
            Row(signature.Id, SignalStatus.Recorded),
            Row(signature.Id, SignalStatus.Suppressed),
            Row(signature.Id, SignalStatus.Deduplicated),
            Row(signature.Id, SignalStatus.Promoted)
        );

        var stats = await _handler.Handle(new GetTelemetryStatsQuery(), CancellationToken.None);

        Assert.Equal(5, stats.Funnel.Signals);
        Assert.Equal(3, stats.Funnel.NotRaised);
    }

    [Fact]
    public async Task EveryBandAppears_IncludingTheEmptyOnes()
    {
        var signature = Signature();
        GivenSignatures(signature);
        GivenSignalRows(Row(signature.Id, SignalStatus.Promoted));

        var stats = await _handler.Handle(new GetTelemetryStatsQuery(), CancellationToken.None);

        Assert.Equal(
            Enum.GetValues<SignalStatus>().Length,
            stats.Funnel.SignalsByStatus.Count
        );
        Assert.Equal(0, stats.Funnel.SignalsByStatus["Weak"]);
        Assert.Equal(1, stats.Funnel.SignalsByStatus["Promoted"]);
    }

    [Fact]
    public async Task TheFunnelNarrowsFromRecordsToSignatures()
    {
        // The drop between these two is the whole return on fingerprinting: two hundred identical
        // failures are one problem, and the screen has to be able to show that.
        GivenLogs(
            new LogWindowSummary(
                200,
                2,
                new Dictionary<string, int> { ["checkout-service"] = 200 },
                new Dictionary<LogSeverity, int> { [LogSeverity.Error] = 200 }
            )
        );

        var stats = await _handler.Handle(new GetTelemetryStatsQuery(), CancellationToken.None);

        Assert.Equal(200, stats.Funnel.LogRecords);
        Assert.Equal(2, stats.Funnel.Signatures);
        Assert.Equal(200, stats.Funnel.LogRecordsBySeverity["Error"]);
    }

    [Fact]
    public async Task AServiceWithErrorsButNoSignalsStillGetsARow()
    {
        // A service can produce errors all week without any of them crossing a detection rule.
        // That is a fact about the service, and dropping the row would report it as healthy.
        GivenLogs(
            new LogWindowSummary(
                50,
                1,
                new Dictionary<string, int> { ["quiet-service"] = 50 },
                new Dictionary<LogSeverity, int>()
            )
        );

        var stats = await _handler.Handle(new GetTelemetryStatsQuery(), CancellationToken.None);

        var row = Assert.Single(stats.Services);
        Assert.Equal("quiet-service", row.Service);
        Assert.Equal(50, row.LogRecords);
        Assert.Equal(0, row.Signals);
        Assert.Null(row.LastSignalAt);
        Assert.Null(row.TopSignature);
    }

    [Fact]
    public async Task ServicesComeFromTheSignature_NotTheSignal()
    {
        // Signal carries a signature id and nothing about where the failure happened; the service
        // lives on the signature. Getting this join wrong puts every row under one heading.
        var checkout = Signature("checkout-service");
        var search = Signature("search-service");

        GivenSignatures(checkout, search);
        GivenSignalRows(
            Row(checkout.Id, SignalStatus.Promoted),
            Row(search.Id),
            Row(search.Id)
        );

        var stats = await _handler.Handle(new GetTelemetryStatsQuery(), CancellationToken.None);

        Assert.Equal(2, stats.Services.Count);
        Assert.Equal(1, stats.Services.Single(x => x.Service == "checkout-service").Promoted);
        Assert.Equal(2, stats.Services.Single(x => x.Service == "search-service").Signals);
    }

    [Fact]
    public async Task ASignalWhoseSignatureIsGoneIsGroupedRatherThanDropped()
    {
        // A silently shorter table is the kind of wrong nobody notices, so the row survives under
        // a name that says what happened to it.
        GivenSignatures();
        GivenSignalRows(Row(Guid.NewGuid()));

        var stats = await _handler.Handle(new GetTelemetryStatsQuery(), CancellationToken.None);

        var row = Assert.Single(stats.Services);
        Assert.Equal(1, row.Signals);
        Assert.Null(row.TopSignature);
    }

    [Fact]
    public async Task TheTopSignatureIsTheOneWithTheMostOccurrences_NotTheMostSignals()
    {
        // Three quiet signals are not a bigger problem than one storm, and the number on the row
        // has to agree with the heat map, which sizes by occurrences.
        var noisy = Signature(exceptionType: "OutOfMemoryException");
        var chatty = Signature(exceptionType: "TimeoutException");

        GivenSignatures(noisy, chatty);
        GivenSignalRows(
            Row(noisy.Id, occurrences: 300),
            Row(chatty.Id, occurrences: 5),
            Row(chatty.Id, occurrences: 5),
            Row(chatty.Id, occurrences: 5)
        );

        var row = Assert.Single(
            (await _handler.Handle(new GetTelemetryStatsQuery(), CancellationToken.None)).Services
        );

        Assert.Equal("OutOfMemoryException", row.TopSignature);
        Assert.Equal(300, row.TopSignatureOccurrences);
    }

    [Fact]
    public async Task IncidentsAreCountedOnce_HoweverManySignalsPointAtThem()
    {
        var signature = Signature();
        var incident = Guid.NewGuid();

        GivenSignatures(signature);
        GivenSignalRows(
            Row(signature.Id, SignalStatus.Promoted, incident),
            Row(signature.Id, SignalStatus.Deduplicated, incident),
            Row(signature.Id, SignalStatus.Weak)
        );

        var row = Assert.Single(
            (await _handler.Handle(new GetTelemetryStatsQuery(), CancellationToken.None)).Services
        );

        Assert.Equal(1, row.Incidents);
    }

    [Fact]
    public async Task AnOverwideWindowIsClamped()
    {
        await _handler.Handle(
            new GetTelemetryStatsQuery(Noon.AddYears(-1), Noon),
            CancellationToken.None
        );

        await _signals
            .Received(1)
            .GetWindowStatsRowsAsync(
                Noon - GetTelemetryStatsQueryHandler.MaxWindow,
                Noon,
                Arg.Any<CancellationToken>()
            );
    }
}
