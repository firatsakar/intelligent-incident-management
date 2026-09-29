using BuildingBlocks.EventBus;
using BuildingBlocks.SharedKernel;
using IncidentService.Application.Abstractions;
using IncidentService.Application.Commands.AddIncidentComment;
using IncidentService.Application.Commands.ApplyAiAnalysis;
using IncidentService.Application.Commands.AssignTeam;
using IncidentService.Application.Commands.CreateIncident;
using IncidentService.Application.Commands.CreateIncidentFromSignal;
using IncidentService.Application.Commands.RecordAiAnalysisFailure;
using IncidentService.Application.Commands.UpdateIncidentStatus;
using IncidentService.Application.DTOs;
using IncidentService.Application.Queries.GetIncidentActivity;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Exceptions;
using IncidentService.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IncidentService.Tests;

// Every path that changes an incident leaves exactly one row saying what changed and who
// did it — and a path that changes nothing, or a message delivered twice, leaves none.
public sealed class IncidentActivityTests
{
    private static readonly Guid Organization = Guid.NewGuid();
    private static readonly Guid PersonId = Guid.NewGuid();

    private readonly IIncidentRepository _incidents = Substitute.For<IIncidentRepository>();
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
    private readonly RecordedActivity _activity = new();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();

    public IncidentActivityTests()
    {
        _user.Id.Returns(PersonId);
        _user.Name.Returns("Ayşe Operator");
    }

    private Incident Stored(IncidentSource source = IncidentSource.Telemetry)
    {
        var incident = Incident.Create(
            Organization,
            "checkout-service: TimeoutException",
            "Payment gateway stopped responding.",
            IncidentPriority.Medium,
            source
        );

        _incidents.GetByIdAsync(incident.Id, Arg.Any<CancellationToken>()).Returns(incident);

        return incident;
    }

    // ---- the rows themselves -----------------------------------------------------------------

    [Fact]
    public void ARowBelongsToTheIncidentsOrganisationAndCopiesTheActorsName()
    {
        var incident = Stored();

        var row = IncidentActivity.Opened(incident, ActivityActor.ApiKey("grafana-prod"));

        Assert.Equal(Organization, row.OrganizationId);
        Assert.Equal(incident.Id, row.IncidentId);
        Assert.Equal(IncidentActivityKind.Opened, row.Kind);
        Assert.Equal(ActivityActorKind.ApiKey, row.ActorKind);
        Assert.Equal("grafana-prod", row.ActorName);
        Assert.Null(row.ActorId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void ACommentNeedsText(string text)
    {
        Assert.Throws<ArgumentException>(() =>
            IncidentActivity.Commented(Stored(), text, ActivityActor.User(PersonId, "Ayşe"))
        );
    }

    [Fact]
    public void ACommentIsKeptAsWrittenApartFromTheSurroundingWhitespace()
    {
        var row = IncidentActivity.Commented(Stored(), "  Rolled back 4f2c.\nWatching.  ", ActivityActor.User(PersonId, "Ayşe"));

        Assert.Equal("Rolled back 4f2c.\nWatching.", row.Text);
        Assert.Throws<ArgumentException>(() =>
            IncidentActivity.Commented(Stored(), new string('x', IncidentActivity.TextMaxLength + 1), ActivityActor.Ai)
        );
    }

    [Fact]
    public void AnAnalysisErrorLongerThanTheColumnIsClippedRatherThanRefused()
    {
        var incident = Stored();
        incident.RecordAiAnalysisFailure(new string('e', IncidentActivity.TextMaxLength + 500));

        var row = IncidentActivity.AnalysisFailed(incident);

        Assert.Equal(IncidentActivity.TextMaxLength, row.Text!.Length);
    }

    // ---- status ------------------------------------------------------------------------------

    private UpdateIncidentStatusCommandHandler StatusHandler() => new(_incidents, _realtime, _activity, _user);

    [Fact]
    public async Task ClosingRecordsTheMoveTheVerdictAndWhoDidIt()
    {
        var incident = Stored();

        await StatusHandler().Handle(
            new UpdateIncidentStatusCommand
            {
                IncidentId = incident.Id,
                NewStatus = IncidentStatus.Resolved,
                Verdict = IncidentVerdict.FalsePositive,
            },
            CancellationToken.None
        );

        var row = Assert.Single(_activity.Rows);
        Assert.Equal(IncidentActivityKind.StatusChanged, row.Kind);
        Assert.Equal("Open", row.From);
        Assert.Equal("Resolved", row.To);
        Assert.Equal(IncidentVerdict.FalsePositive, row.Verdict);
        Assert.Equal(ActivityActorKind.User, row.ActorKind);
        Assert.Equal(PersonId, row.ActorId);
        Assert.Equal("Ayşe Operator", row.ActorName);
    }

    [Fact]
    public async Task SettingTheStatusItAlreadyHasRecordsNothing()
    {
        var incident = Stored();

        await StatusHandler().Handle(
            new UpdateIncidentStatusCommand { IncidentId = incident.Id, NewStatus = IncidentStatus.Open },
            CancellationToken.None
        );

        Assert.Empty(_activity.Rows);
        await _realtime.DidNotReceive().ActivityRecordedAsync(Arg.Any<IncidentActivityDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARecordedChangeReachesOpenScreensAfterTheSave()
    {
        var incident = Stored();

        await StatusHandler().Handle(
            new UpdateIncidentStatusCommand { IncidentId = incident.Id, NewStatus = IncidentStatus.InProgress },
            CancellationToken.None
        );

        Received.InOrder(() =>
        {
            _incidents.SaveChangesAsync(Arg.Any<CancellationToken>());
            _realtime.ActivityRecordedAsync(
                Arg.Is<IncidentActivityDto>(dto => dto.Kind == IncidentActivityKind.StatusChanged && dto.To == "InProgress"),
                Arg.Any<CancellationToken>()
            );
        });
    }

    // ---- team --------------------------------------------------------------------------------

    [Fact]
    public async Task AssigningRecordsBothTeamsAndRepeatingItRecordsNothing()
    {
        var incident = Stored();
        var handler = new AssignTeamCommandHandler(_incidents, _realtime, _activity, _user);

        await handler.Handle(new AssignTeamCommand { IncidentId = incident.Id, Team = "payments" }, CancellationToken.None);
        await handler.Handle(new AssignTeamCommand { IncidentId = incident.Id, Team = "platform" }, CancellationToken.None);
        await handler.Handle(new AssignTeamCommand { IncidentId = incident.Id, Team = "platform" }, CancellationToken.None);

        Assert.Collection(
            _activity.Rows,
            first => Assert.Equal((null, "payments"), (first.From, first.To)),
            second => Assert.Equal(("payments", "platform"), (second.From, second.To))
        );
    }

    // ---- the analysis ------------------------------------------------------------------------

    [Fact]
    public async Task AnAnalysisIsRecordedOnceWithThePriorityItSetEvenWhenDeliveredTwice()
    {
        var incident = Stored();
        var handler = new ApplyAiAnalysisCommandHandler(_incidents, _realtime, _activity);
        var result = new ApplyAiAnalysisCommand
        {
            IncidentId = incident.Id,
            SuggestedPriority = "Critical",
            SuggestedCategory = "Database",
            Reasoning = "Connection pool exhausted.",
            Confidence = 0.8,
        };

        await handler.Handle(result, CancellationToken.None);
        await handler.Handle(result, CancellationToken.None);

        var row = Assert.Single(_activity.Rows);
        Assert.Equal(IncidentActivityKind.AnalysisApplied, row.Kind);
        Assert.Equal(ActivityActorKind.Ai, row.ActorKind);
        Assert.Equal(("Medium", "Critical"), (row.From, row.To));
        Assert.Equal("Database", row.Text);
    }

    [Fact]
    public async Task TheSameFailureDeliveredTwiceIsRecordedOnce()
    {
        var incident = Stored();
        var handler = new RecordAiAnalysisFailureCommandHandler(_incidents, _realtime, _activity);
        var failure = new RecordAiAnalysisFailureCommand { IncidentId = incident.Id, Error = "rate limit exceeded" };

        await handler.Handle(failure, CancellationToken.None);
        await handler.Handle(failure, CancellationToken.None);

        var row = Assert.Single(_activity.Rows);
        Assert.Equal(IncidentActivityKind.AnalysisFailed, row.Kind);
        Assert.Equal("rate limit exceeded", row.Text);
    }

    // ---- opening -----------------------------------------------------------------------------

    private CreateIncidentCommandHandler CreateHandler()
    {
        var organization = new OrganizationContext();
        organization.Set(Organization);

        return new CreateIncidentCommandHandler(
            _incidents,
            Substitute.For<IEventBus>(),
            _realtime,
            organization,
            _activity,
            _user
        );
    }

    [Fact]
    public async Task AnIncidentSentWithAKeyIsOpenedByTheKey()
    {
        await CreateHandler().Handle(
            new CreateIncidentCommand
            {
                Title = "Disk full",
                Description = "db-2 at 100%",
                Priority = IncidentPriority.High,
                Source = IncidentSource.Alert,
                ReportedBy = "grafana-prod",
            },
            CancellationToken.None
        );

        var row = Assert.Single(_activity.Rows);
        Assert.Equal(IncidentActivityKind.Opened, row.Kind);
        Assert.Equal(ActivityActorKind.ApiKey, row.ActorKind);
        Assert.Equal("grafana-prod", row.ActorName);
    }

    [Fact]
    public async Task AnIncidentOpenedWithoutAKeyIsOpenedByWhoeverIsSignedIn()
    {
        await CreateHandler().Handle(
            new CreateIncidentCommand
            {
                Title = "Disk full",
                Description = "db-2 at 100%",
                Priority = IncidentPriority.High,
                Source = IncidentSource.Manual,
            },
            CancellationToken.None
        );

        var row = Assert.Single(_activity.Rows);
        Assert.Equal(ActivityActorKind.User, row.ActorKind);
        Assert.Equal(PersonId, row.ActorId);
    }

    [Fact]
    public async Task ATelemetryPromotionIsOpenedByTheDetector()
    {
        var organization = new OrganizationContext();
        organization.Set(Organization);

        var handler = new CreateIncidentFromSignalCommandHandler(
            _incidents,
            Substitute.For<IEventBus>(),
            _realtime,
            NullLogger<CreateIncidentFromSignalCommandHandler>.Instance,
            organization,
            _activity
        );

        await handler.Handle(
            new CreateIncidentFromSignalCommand
            {
                IncidentId = Guid.NewGuid(),
                Title = "checkout-service: TimeoutException",
                Description = "Detected automatically from telemetry.",
                Severity = "High",
                DetectedAt = DateTime.UtcNow.AddMinutes(-5),
            },
            CancellationToken.None
        );

        var row = Assert.Single(_activity.Rows);
        Assert.Equal(ActivityActorKind.Detector, row.ActorKind);
        Assert.Null(row.ActorName);
    }

    // ---- comments ----------------------------------------------------------------------------

    [Fact]
    public async Task ACommentIsSavedAsTheSignedInPersonAndAnnounced()
    {
        var incident = Stored();
        var handler = new AddIncidentCommentCommandHandler(_incidents, _activity, _realtime, _user);

        var dto = await handler.Handle(new AddIncidentCommentCommand(incident.Id, " Rolled back. "), CancellationToken.None);

        var row = Assert.Single(_activity.Rows);
        Assert.Equal(IncidentActivityKind.Commented, row.Kind);
        Assert.Equal("Rolled back.", row.Text);
        Assert.Equal((ActivityActorKind.User, PersonId, "Ayşe Operator"), (row.ActorKind, row.ActorId, row.ActorName));
        Assert.Equal(row.Id, dto.Id);

        await _incidents.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _realtime.Received(1).ActivityRecordedAsync(Arg.Is<IncidentActivityDto>(x => x.Id == row.Id), Arg.Any<CancellationToken>());

        // A comment is about the people working it, not a change to the incident.
        Assert.Null(incident.UpdatedAt);
    }

    [Fact]
    public async Task ACommentOnAnIncidentThatIsNotThereIsNotFoundAndNothingIsSaved()
    {
        var handler = new AddIncidentCommentCommandHandler(_incidents, _activity, _realtime, _user);

        await Assert.ThrowsAsync<IncidentNotFoundException>(() =>
            handler.Handle(new AddIncidentCommentCommand(Guid.NewGuid(), "Hello"), CancellationToken.None)
        );

        Assert.Empty(_activity.Rows);
        await _incidents.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Looking at the pool now.", true)]
    public void ACommentIsRefusedBeforeTheHandlerWhenItHasNoText(string text, bool valid)
    {
        var result = new AddIncidentCommentCommandValidator().Validate(new AddIncidentCommentCommand(Guid.NewGuid(), text));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void ACommentLongerThanTheLimitIsRefusedButSurroundingWhitespaceDoesNotCount()
    {
        var validator = new AddIncidentCommentCommandValidator();
        var atLimit = new string('x', IncidentActivity.TextMaxLength);

        Assert.True(validator.Validate(new AddIncidentCommentCommand(Guid.NewGuid(), $"  {atLimit}\n")).IsValid);
        Assert.False(validator.Validate(new AddIncidentCommentCommand(Guid.NewGuid(), atLimit + "x")).IsValid);
    }

    // ---- reading -----------------------------------------------------------------------------

    [Fact]
    public async Task AnIncidentThatIsNotThereHasNoHistoryRatherThanAnEmptyOne()
    {
        var handler = new GetIncidentActivityQueryHandler(_incidents, _activity);

        await Assert.ThrowsAsync<IncidentNotFoundException>(() =>
            handler.Handle(new GetIncidentActivityQuery(Guid.NewGuid()), CancellationToken.None)
        );
    }

    [Fact]
    public async Task TheHistoryIsReadInTheOrderItHappened()
    {
        var incident = Stored();
        _activity.Add(IncidentActivity.Opened(incident, ActivityActor.Detector));
        _activity.Add(IncidentActivity.Commented(incident, "Looking.", ActivityActor.User(PersonId, "Ayşe")));

        var rows = await new GetIncidentActivityQueryHandler(_incidents, _activity).Handle(
            new GetIncidentActivityQuery(incident.Id),
            CancellationToken.None
        );

        Assert.Equal([IncidentActivityKind.Opened, IncidentActivityKind.Commented], rows.Select(x => x.Kind));
        Assert.Equal("Looking.", rows[1].Text);
    }

    private sealed class RecordedActivity : IIncidentActivityRepository
    {
        public List<IncidentActivity> Rows { get; } = [];

        public void Add(IncidentActivity activity) => Rows.Add(activity);

        public Task<IReadOnlyList<IncidentActivity>> ListForIncidentAsync(
            Guid incidentId,
            int limit,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<IncidentActivity>>(Rows.Where(x => x.IncidentId == incidentId).TakeLast(limit).ToList());
    }
}
