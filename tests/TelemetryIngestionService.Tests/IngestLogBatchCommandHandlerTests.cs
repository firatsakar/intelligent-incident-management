using BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.DetectSignals;
using TelemetryIngestionService.Application.Commands.IngestLogBatch;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Tests;

// The pipeline both the poller and the OTLP endpoint hand their batches to. What matters here is
// the seam between rows and events: rows are samples, but every count downstream is of events.
public sealed class IngestLogBatchCommandHandlerTests
{
    private static readonly Guid Source = Guid.NewGuid();
    private static readonly DateTime Origin = DateTime.UtcNow.AddMinutes(-5);

    private readonly ILogRecordRepository _logRecords = Substitute.For<ILogRecordRepository>();
    private readonly IErrorSignatureRepository _signatures = Substitute.For<IErrorSignatureRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
    private readonly OrganizationContext _organization = new();

    private readonly List<LogRecord> _stored = [];
    private readonly List<ErrorSignature> _createdSignatures = [];

    public IngestLogBatchCommandHandlerTests()
    {
        _organization.Set(Guid.NewGuid());

        _logRecords
            .GetExistingSourceEventIdsAsync(Source, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());

        _logRecords
            .When(x => x.AddRangeAsync(Arg.Any<IEnumerable<LogRecord>>(), Arg.Any<CancellationToken>()))
            .Do(call => _stored.AddRange(call.Arg<IEnumerable<LogRecord>>()));

        _signatures
            .GetByFingerprintsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _signatures
            .When(x => x.AddAsync(Arg.Any<ErrorSignature>(), Arg.Any<CancellationToken>()))
            .Do(call => _createdSignatures.Add(call.Arg<ErrorSignature>()));
    }

    private IngestLogBatchCommandHandler Handler() =>
        new(
            _logRecords,
            _signatures,
            _sender,
            _realtime,
            NullLogger<IngestLogBatchCommandHandler>.Instance,
            _organization
        );

    private static RawLogEvent Checkout(int second, string? id = null, LogSeverity severity = LogSeverity.Error) =>
        new()
        {
            SourceEventId = id ?? $"evt-{second}",
            Timestamp = Origin.AddSeconds(second),
            Severity = severity,
            Service = "checkout-service",
            Message = $"Checkout failed for order {100000 + second}",
            MessageTemplate = "Checkout failed for order {OrderId}",
            ExceptionType = "InvalidOperationException",
        };

    [Fact]
    public async Task AStormIsStoredAsSamplesThatStillWeighEveryEvent()
    {
        var storm = Enumerable.Range(0, 300).Select(i => Checkout(i)).ToList();

        var result = await Handler().Handle(new IngestLogBatchCommand(Source, storm), CancellationToken.None);

        Assert.Equal(10, _stored.Count);
        Assert.Equal(300, _stored.Sum(x => x.Occurrences));
        Assert.Equal(10, result.Stored);
        Assert.Equal(290, result.Folded);
        Assert.Equal(1, result.SignaturesTouched);
    }

    [Fact]
    public async Task TheSignatureCountsEventsNotRows()
    {
        var storm = Enumerable.Range(0, 300).Select(i => Checkout(i)).ToList();

        await Handler().Handle(new IngestLogBatchCommand(Source, storm), CancellationToken.None);

        var signature = Assert.Single(_createdSignatures);
        Assert.Equal(300, signature.OccurrenceCount);
        Assert.Equal(Origin, signature.FirstSeenAt);
        Assert.Equal(Origin.AddSeconds(299), signature.LastSeenAt);
    }

    [Fact]
    public async Task DetectionRunsOnTheTouchedSignature()
    {
        await Handler().Handle(
            new IngestLogBatchCommand(Source, [Checkout(0), Checkout(1)]),
            CancellationToken.None
        );

        await _sender.Received(1).Send(
            Arg.Is<DetectSignalsCommand>(command => command.Fingerprints.Count == 1),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task WhatIsAlreadyHeldIsDroppedBeforeFolding()
    {
        _logRecords
            .GetExistingSourceEventIdsAsync(Source, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { "evt-0" });

        var result = await Handler().Handle(
            new IngestLogBatchCommand(Source, [Checkout(0), Checkout(1)]),
            CancellationToken.None
        );

        Assert.Equal(1, result.Duplicates);
        Assert.Equal("evt-1", Assert.Single(_stored).SourceEventId);
    }

    [Fact]
    public async Task TheSameEventTwiceInOneBatchIsStoredOnce()
    {
        // A pushed batch can repeat itself, and two rows with one event id would break the unique
        // index and fail the whole batch rather than just the repeat.
        var result = await Handler().Handle(
            new IngestLogBatchCommand(Source, [Checkout(0, "same"), Checkout(1, "same")]),
            CancellationToken.None
        );

        Assert.Single(_stored);
        Assert.Equal(1, result.Duplicates);
    }

    [Fact]
    public async Task ContextBelowErrorIsStoredButNeverFingerprinted()
    {
        var result = await Handler().Handle(
            new IngestLogBatchCommand(Source, [Checkout(0, severity: LogSeverity.Warning)]),
            CancellationToken.None
        );

        Assert.Null(Assert.Single(_stored).Fingerprint);
        Assert.Equal(0, result.SignaturesTouched);
        await _sender.DidNotReceive().Send(Arg.Any<DetectSignalsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnEmptyBatchTouchesNothing()
    {
        var result = await Handler().Handle(new IngestLogBatchCommand(Source, []), CancellationToken.None);

        Assert.Equal(0, result.Received);
        await _logRecords.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
