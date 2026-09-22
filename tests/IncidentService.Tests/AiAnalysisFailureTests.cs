using IncidentService.Application.Abstractions;
using IncidentService.Application.Commands.ApplyAiAnalysis;
using IncidentService.Application.Commands.RecordAiAnalysisFailure;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using NSubstitute;

namespace IncidentService.Tests;

// A failed analysis used to be silent end to end: MarkAsFailed raised no domain event, so nothing
// reached the outbox, nothing reached this service, and nothing reached a screen. The incident sat
// on "analysis pending" for the rest of its life — the one wrong state that never corrects itself,
// because the thing that would have corrected it is what failed.
public sealed class AiAnalysisFailureTests
{
    private readonly IIncidentRepository _repository = Substitute.For<IIncidentRepository>();
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();

    private static Incident AnIncident() =>
        Incident.Create(
            "checkout-service: TimeoutException",
            "Payment for order failed",
            IncidentPriority.Medium,
            IncidentSource.Telemetry
        );

    private RecordAiAnalysisFailureCommandHandler HandlerFor(Incident incident)
    {
        _repository.GetByIdAsync(incident.Id, Arg.Any<CancellationToken>()).Returns(incident);

        return new RecordAiAnalysisFailureCommandHandler(_repository, _realtime);
    }

    [Fact]
    public void FailingIsNotTheSameAsNotHavingRunYet()
    {
        // The distinction the field exists for. IsAiAnalyzed stays false because no analysis was
        // applied — the flag means "these AI fields hold something", and here they do not — but
        // the error says the attempt happened, which is what stops the screen waiting forever.
        var incident = AnIncident();

        Assert.False(incident.IsAiAnalyzed);
        Assert.Null(incident.AiAnalysisError);

        incident.RecordAiAnalysisFailure("rate limit exceeded");

        Assert.False(incident.IsAiAnalyzed);
        Assert.Equal("rate limit exceeded", incident.AiAnalysisError);
    }

    [Fact]
    public void ALaterSuccessClearsAnEarlierFailure()
    {
        // Analysis is retried through redelivery, so an incident that failed once and then
        // succeeded must not keep wearing the failure.
        var incident = AnIncident();
        incident.RecordAiAnalysisFailure("rate limit exceeded");

        incident.ApplyAiAnalysis(IncidentPriority.High, "Application", "because", 0.9);

        Assert.Null(incident.AiAnalysisError);
        Assert.True(incident.IsAiAnalyzed);
    }

    [Fact]
    public void ARedeliveredFailureIsNotASecondDifferentOne()
    {
        // At-least-once delivery means this handler will see the same failure twice.
        var incident = AnIncident();

        incident.RecordAiAnalysisFailure("rate limit exceeded");
        incident.RecordAiAnalysisFailure("rate limit exceeded");

        Assert.Equal("rate limit exceeded", incident.AiAnalysisError);
    }

    [Fact]
    public async Task TheFailureReachesOpenScreensThroughTheExistingPush()
    {
        // No new hub and no new message type: an incident whose analysis failed is an incident
        // that changed, so incidentChanged already says everything that needs saying.
        var incident = AnIncident();
        var handler = HandlerFor(incident);

        await handler.Handle(
            new RecordAiAnalysisFailureCommand
            {
                IncidentId = incident.Id,
                Error = "invalid API key",
            },
            CancellationToken.None
        );

        await _realtime
            .Received(1)
            .IncidentChangedAsync(
                Arg.Is<IncidentDto>(dto =>
                    dto.Id == incident.Id
                    && dto.AiAnalysisError == "invalid API key"
                    && !dto.IsAiAnalyzed
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task TheAnnouncementComesAfterTheSave()
    {
        var incident = AnIncident();
        var handler = HandlerFor(incident);

        await handler.Handle(
            new RecordAiAnalysisFailureCommand
            {
                IncidentId = incident.Id,
                Error = "invalid API key",
            },
            CancellationToken.None
        );

        Received.InOrder(() =>
        {
            _repository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _realtime.IncidentChangedAsync(Arg.Any<IncidentDto>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task TheErrorTravelsVerbatim()
    {
        // The provider's own words, not a category. "rate limit exceeded" and "invalid API key"
        // call for different actions, and a shared label would hide that from the one person who
        // has to choose between them.
        var incident = AnIncident();
        var handler = HandlerFor(incident);

        const string provider =
            "The model is overloaded. Please retry your request after a short delay.";

        await handler.Handle(
            new RecordAiAnalysisFailureCommand { IncidentId = incident.Id, Error = provider },
            CancellationToken.None
        );

        Assert.Equal(provider, incident.AiAnalysisError);
    }
}
