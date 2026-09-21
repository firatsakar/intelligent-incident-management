using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using IncidentService.Application.Abstractions;
using IncidentService.Application.Commands.CreateIncidentFromSignal;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IncidentService.Tests;

// Where a telemetry promotion becomes an incident. The seam that has to survive at-least-once
// delivery without opening the same incident twice.
public sealed class CreateIncidentFromSignalCommandHandlerTests
{
    private static readonly Guid IncidentId = Guid.NewGuid();
    private static readonly DateTime DetectedAt = DateTime.UtcNow.AddMinutes(-15);

    private readonly IIncidentRepository _repository = Substitute.For<IIncidentRepository>();
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
    private readonly CreateIncidentFromSignalCommandHandler _handler;

    public CreateIncidentFromSignalCommandHandlerTests()
    {
        _handler = new CreateIncidentFromSignalCommandHandler(
            _repository,
            _eventBus,
            NullLogger<CreateIncidentFromSignalCommandHandler>.Instance
        );
    }

    private Task Handle(string severity = "High") =>
        _handler.Handle(
            new CreateIncidentFromSignalCommand
            {
                IncidentId = IncidentId,
                Title = "checkout-service: TimeoutException",
                Description = "Detected automatically from telemetry.",
                Severity = severity,
                DetectedAt = DetectedAt,
            },
            CancellationToken.None
        );

    [Fact]
    public async Task OpensAnIncidentWithTheIdTheSignalChose()
    {
        Incident? added = null;
        _repository
            .When(r => r.AddAsync(Arg.Any<Incident>(), Arg.Any<CancellationToken>()))
            .Do(call => added = call.Arg<Incident>());

        await Handle();

        Assert.NotNull(added);
        Assert.Equal(IncidentId, added.Id);
        Assert.Equal(IncidentSource.Telemetry, added.Source);
        Assert.Equal(DetectedAt, added.DetectedAt);
        Assert.Equal(IncidentPriority.High, added.Priority);
    }

    [Fact]
    public async Task ARedeliveredPromotionIsANoOp()
    {
        // The whole reason the id is chosen on the telemetry side. Without this the second
        // delivery of the same promotion opens a second incident for one burst.
        _repository
            .GetByIdAsync(IncidentId, Arg.Any<CancellationToken>())
            .Returns(
                Incident.Create(
                    "already here",
                    "…",
                    IncidentPriority.High,
                    IncidentSource.Telemetry,
                    id: IncidentId
                )
            );

        await Handle();

        await _repository.DidNotReceive().AddAsync(Arg.Any<Incident>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        // And no second announcement, which would re-run analysis and re-send notifications.
        await _eventBus
            .DidNotReceive()
            .PublishAsync(Arg.Any<IncidentDetectedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandsTheIncidentToTheExistingChain()
    {
        IncidentDetectedEvent? published = null;
        _eventBus
            .When(bus =>
                bus.PublishAsync(Arg.Any<IncidentDetectedEvent>(), Arg.Any<CancellationToken>())
            )
            .Do(call => published = call.Arg<IncidentDetectedEvent>());

        await Handle();

        Assert.NotNull(published);
        Assert.Equal(IncidentId, published.IncidentId);
        Assert.Equal(nameof(IncidentSource.Telemetry), published.Source);
        Assert.Equal(DetectedAt, published.DetectedAt);
    }

    [Fact]
    public async Task TheIncidentIsSavedBeforeItIsAnnounced()
    {
        // Announcing first would let the analysis service ask for an incident that is not there
        // yet.
        await Handle();

        Received.InOrder(() =>
        {
            _repository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _eventBus.PublishAsync(
                Arg.Any<IncidentDetectedEvent>(),
                Arg.Any<CancellationToken>()
            );
        });
    }

    [Theory]
    [InlineData("Critical", IncidentPriority.Critical)]
    [InlineData("high", IncidentPriority.High)]
    // Telemetry's severity is only a starting point, and an unrecognised one must not stop an
    // incident being opened — the AI analysis that follows sets the real priority anyway.
    [InlineData("Catastrophic", IncidentPriority.Medium)]
    [InlineData("", IncidentPriority.Medium)]
    public async Task SeverityIsParsedLenientlyAndFallsBackToMedium(
        string severity,
        IncidentPriority expected
    )
    {
        Incident? added = null;
        _repository
            .When(r => r.AddAsync(Arg.Any<Incident>(), Arg.Any<CancellationToken>()))
            .Do(call => added = call.Arg<Incident>());

        await Handle(severity);

        Assert.Equal(expected, added!.Priority);
    }
}
