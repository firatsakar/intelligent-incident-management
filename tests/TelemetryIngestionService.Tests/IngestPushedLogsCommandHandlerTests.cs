using BuildingBlocks.SharedKernel;
using MediatR;
using NSubstitute;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.IngestLogBatch;
using TelemetryIngestionService.Application.Commands.IngestPushedLogs;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Tests;

public sealed class IngestPushedLogsCommandHandlerTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly ITelemetrySourceRepository _sources = Substitute.For<ITelemetrySourceRepository>();
    private readonly ISourceCursorRepository _cursors = Substitute.For<ISourceCursorRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Guid _organization = Guid.NewGuid();
    private IngestLogBatchCommand? _ingested;

    public IngestPushedLogsCommandHandlerTests()
    {
        _sender
            .Send(Arg.Do<IngestLogBatchCommand>(command => _ingested = command), Arg.Any<CancellationToken>())
            .Returns(IngestResult.Nothing());
    }

    private (TelemetrySource Source, SourceCursor Cursor) Given(Dictionary<string, string> config)
    {
        var source = TelemetrySource.Create(_organization, "collector", TelemetrySourceKind.Otlp, config);
        var cursor = SourceCursor.Start(_organization, source.Id);

        _sources.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _cursors.GetOrCreateAsync(source.Id, Arg.Any<CancellationToken>()).Returns(cursor);

        return (source, cursor);
    }

    private static RawLogEvent Event(LogSeverity severity, int secondsAgo = 0) =>
        new()
        {
            Timestamp = Now.AddSeconds(-secondsAgo),
            Severity = severity,
            Service = "checkout",
            Message = "Checkout failed",
        };

    private IngestPushedLogsCommandHandler Handler() => new(_sources, _cursors, _sender);

    [Fact]
    public async Task EverythingBelowErrorIsDroppedByDefault()
    {
        var (source, _) = Given([]);

        var result = await Handler().Handle(
            new IngestPushedLogsCommand(source.Id, [Event(LogSeverity.Information), Event(LogSeverity.Warning), Event(LogSeverity.Error), Event(LogSeverity.Fatal)]),
            CancellationToken.None
        );

        Assert.Equal(2, result.BelowMinimum);
        Assert.Equal([LogSeverity.Error, LogSeverity.Fatal], _ingested!.Events.Select(x => x.Severity));
    }

    [Fact]
    public async Task ASourceCanAskForWarningsToo()
    {
        var (source, _) = Given(new Dictionary<string, string> { ["MinimumSeverity"] = "Warning" });

        await Handler().Handle(
            new IngestPushedLogsCommand(source.Id, [Event(LogSeverity.Information), Event(LogSeverity.Warning)]),
            CancellationToken.None
        );

        Assert.Equal([LogSeverity.Warning], _ingested!.Events.Select(x => x.Severity));
    }

    [Fact]
    public async Task AReceiptIsRecordedOnTheCursor()
    {
        // What a pushed source's test and settings row report: that something arrived, and when.
        var (source, cursor) = Given([]);

        await Handler().Handle(
            new IngestPushedLogsCommand(source.Id, [Event(LogSeverity.Error, secondsAgo: 30), Event(LogSeverity.Error, secondsAgo: 5)]),
            CancellationToken.None
        );

        Assert.NotNull(cursor.LastPolledAt);
        Assert.Equal(Now.AddSeconds(-5), cursor.LastEventTimestamp);
        await _cursors.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ABatchThatWasAllBelowTheFloorStillCountsAsAReceipt()
    {
        // The sender is working; it is only sending more than this source keeps.
        var (source, cursor) = Given([]);

        await Handler().Handle(new IngestPushedLogsCommand(source.Id, [Event(LogSeverity.Debug)]), CancellationToken.None);

        Assert.NotNull(cursor.LastPolledAt);
        Assert.Null(cursor.LastEventTimestamp);
    }
}
