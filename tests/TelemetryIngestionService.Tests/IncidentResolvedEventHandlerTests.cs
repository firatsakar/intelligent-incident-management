using BuildingBlocks.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Application.EventHandlers;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Tests;

// Where a closed incident teaches the signature that opened it. Before this existed nothing
// called DetachIncident, so every signature's record stayed empty and a closed incident kept
// absorbing new bursts of the same error.
public sealed class IncidentResolvedEventHandlerTests
{
    private static readonly Guid Organization = Guid.NewGuid();
    private static readonly DateTime Noon = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly IErrorSignatureRepository _signatures = Substitute.For<IErrorSignatureRepository>();
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
    private readonly IncidentResolvedEventHandler _handler;

    private readonly Guid _incidentId = Guid.NewGuid();
    private readonly ErrorSignature _signature = ErrorSignature.Create(
        Organization,
        "abc123",
        "checkout-service",
        "TimeoutException",
        "Payment for order {OrderId} failed",
        Noon
    );

    public IncidentResolvedEventHandlerTests()
    {
        _handler = new IncidentResolvedEventHandler(
            _signatures,
            _realtime,
            NullLogger<IncidentResolvedEventHandler>.Instance
        );
    }

    private void GivenAttached()
    {
        _signature.AttachIncident(_incidentId, Noon);
        _signatures.GetByCurrentIncidentAsync(_incidentId, Arg.Any<CancellationToken>()).Returns(_signature);
    }

    private Task Resolve(string verdict) =>
        _handler.HandleAsync(
            new IncidentResolvedEvent
            {
                OrganizationId = Organization,
                IncidentId = _incidentId,
                Status = "Resolved",
                Verdict = verdict,
                ResolvedAt = Noon.AddHours(2),
            }
        );

    [Theory]
    [InlineData("Real", 1, 0)]
    [InlineData("FalsePositive", 0, 1)]
    public async Task ReleasesTheSignatureAndCountsTheVerdict(string verdict, int real, int falsePositive)
    {
        GivenAttached();

        await Resolve(verdict);

        Assert.Null(_signature.CurrentIncidentId);
        Assert.Equal(real, _signature.ConfirmedRealCount);
        Assert.Equal(falsePositive, _signature.FalsePositiveCount);

        await _signatures.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _realtime.Received(1).SignatureChangedAsync(
            Arg.Is<ErrorSignatureDto>(dto => dto.Id == _signature.Id && dto.CurrentIncidentId == null),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ARedeliveryFindsNothingAttachedAndCountsNothingTwice()
    {
        GivenAttached();

        await Resolve("FalsePositive");

        // The real repository finds by CurrentIncidentId, which the first delivery cleared; the
        // substitute still hands the signature back, so this also covers the handler's own check.
        await Resolve("FalsePositive");

        Assert.Equal(1, _signature.FalsePositiveCount);
        await _signatures.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnIncidentNoSignatureOpenedIsLeftAlone()
    {
        // A manual incident, or one whose signature has already moved on.
        await Resolve("Real");

        await _signatures.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _realtime.DidNotReceive().SignatureChangedAsync(
            Arg.Any<ErrorSignatureDto>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task AnUnknownVerdictTeachesNothing()
    {
        GivenAttached();

        await Resolve("Maybe");

        Assert.Equal(_incidentId, _signature.CurrentIncidentId);
        Assert.Equal(0, _signature.ConfirmedRealCount + _signature.FalsePositiveCount);
        await _signatures.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
