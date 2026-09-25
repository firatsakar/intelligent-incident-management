using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Events;

namespace IncidentService.Tests;

public sealed class IncidentTests
{
    // One organisation for the whole file. These are unit tests of rules, not of scoping — the
    // filters that make the column matter live in the DbContext — so the value only has to be
    // consistent.
    private static readonly Guid Organization = Guid.NewGuid();
    private static Incident Create(Guid? id = null, DateTime? detectedAt = null) =>
        Incident.Create(
            Organization,
            "checkout-service: TimeoutException",
            "Payment gateway stopped responding.",
            IncidentPriority.Medium,
            IncidentSource.Telemetry,
            assignedTeam: null,
            id: id,
            detectedAt: detectedAt
        );

    [Fact]
    public void Create_OpensTheIncidentAndRaisesNothing()
    {
        var incident = Create();

        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.False(incident.IsAiAnalyzed);

        // Everything an aggregate raises goes to the outbox, and an event no handler claims is a
        // row retried forever. Creation is announced by the handler, as IncidentDetectedEvent.
        Assert.Empty(incident.DomainEvents);
    }

    [Fact]
    public void Create_AcceptsACallerChosenId()
    {
        // A telemetry promotion picks the id so that a redelivered event collides on the primary
        // key instead of opening a second incident for the same burst.
        var id = Guid.NewGuid();

        Assert.Equal(id, Create(id: id).Id);
    }

    [Fact]
    public void Create_GeneratesAnIdWhenTheCallerHasNoOpinion()
    {
        Assert.NotEqual(Guid.Empty, Create().Id);
    }

    [Fact]
    public void DetectedAtIsSeparateFromCreatedAt()
    {
        // When the problem started, as distinct from when the record was filed. An engineer
        // opening an incident at 14:35 for something that began at 14:20 has the same need as a
        // telemetry promotion: correlating evidence against the wrong moment finds nothing.
        var detectedAt = DateTime.UtcNow.AddMinutes(-15);

        var incident = Create(detectedAt: detectedAt);

        Assert.Equal(detectedAt, incident.DetectedAt);
        Assert.True(incident.CreatedAt > incident.DetectedAt);
    }

    [Fact]
    public void DetectedAtIsOptional()
    {
        // Manual creation need not know when the problem started.
        Assert.Null(Create().DetectedAt);
    }

    public sealed class ApplyAiAnalysis
    {
        [Fact]
        public void OverwritesThePriorityAndRecordsTheAnalysis()
        {
            var incident = Create();

            incident.ApplyAiAnalysis(
                IncidentPriority.Critical,
                "Application",
                "Repeated timeouts against the payment gateway.",
                confidence: 0.82
            );

            Assert.Equal(IncidentPriority.Critical, incident.Priority);
            Assert.Equal("Application", incident.AiSuggestedCategory);
            Assert.Equal(
                "Repeated timeouts against the payment gateway.",
                incident.AiReasoning
            );
            Assert.Equal(0.82, incident.AiConfidence);
            Assert.True(incident.IsAiAnalyzed);
        }

        [Fact]
        public void AcceptsAnAnalysisThatGivesNoConfidence()
        {
            // The analysis may decline to put a number on it. That is a missing measurement, not
            // a zero one — rendering it as 0% would read as "certain this is nothing".
            var incident = Create();

            incident.ApplyAiAnalysis(
                IncidentPriority.High,
                "Application",
                "Because.",
                confidence: null
            );

            Assert.Null(incident.AiConfidence);
            Assert.True(incident.IsAiAnalyzed);
        }

        [Fact]
        public void IsIdempotent()
        {
            // The analysis result arrives over an at-least-once bus, so applying it twice has to
            // be indistinguishable from applying it once.
            var incident = Create();

            incident.ApplyAiAnalysis(IncidentPriority.Critical, "Application", "Because.", 0.82);
            var afterFirst = (
                incident.Priority,
                incident.AiSuggestedCategory,
                incident.AiReasoning,
                incident.AiConfidence
            );

            incident.ApplyAiAnalysis(IncidentPriority.Critical, "Application", "Because.", 0.82);

            Assert.Equal(
                afterFirst,
                (
                    incident.Priority,
                    incident.AiSuggestedCategory,
                    incident.AiReasoning,
                    incident.AiConfidence
                )
            );
            Assert.True(incident.IsAiAnalyzed);
        }

        [Fact]
        public void DoesNotReopenOrReassign()
        {
            // Analysis changes what we think the incident is, never where it is in its lifecycle
            // or who owns it.
            var incident = Create();
            incident.AssignTeam("payments");
            incident.UpdateStatus(IncidentStatus.InProgress);

            incident.ApplyAiAnalysis(IncidentPriority.Critical, "Application", "Because.", 0.82);

            Assert.Equal(IncidentStatus.InProgress, incident.Status);
            Assert.Equal("payments", incident.AssignedTeam);
        }
    }

    [Fact]
    public void UpdateStatusAndAssignTeam()
    {
        var incident = Create();

        incident.UpdateStatus(IncidentStatus.Resolved, IncidentVerdict.Real);
        incident.AssignTeam("payments");

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal("payments", incident.AssignedTeam);
        Assert.NotNull(incident.UpdatedAt);
    }

    // The verdict is what telemetry learns from, so when it is asked, and that it is asked only
    // once, is the rule this whole step rests on.
    public sealed class Closing
    {
        private static IReadOnlyList<IncidentResolvedDomainEvent> Resolved(Incident incident) =>
            incident.DomainEvents.OfType<IncidentResolvedDomainEvent>().ToList();

        [Theory]
        [InlineData(IncidentStatus.Resolved)]
        [InlineData(IncidentStatus.Closed)]
        public void AnOpenIncidentCannotBeClosedWithoutAVerdict(IncidentStatus closed)
        {
            var incident = Create();

            Assert.NotNull(incident.StatusChangeProblem(closed, verdict: null));
            Assert.Throws<InvalidOperationException>(() => incident.UpdateStatus(closed));
            Assert.Equal(IncidentStatus.Open, incident.Status);
        }

        [Theory]
        [InlineData(IncidentStatus.Resolved, IncidentVerdict.Real)]
        [InlineData(IncidentStatus.Closed, IncidentVerdict.FalsePositive)]
        public void ClosingRecordsTheVerdictAndAnnouncesItOnce(
            IncidentStatus closed,
            IncidentVerdict verdict
        )
        {
            var incident = Create();
            incident.UpdateStatus(IncidentStatus.InProgress);

            incident.UpdateStatus(closed, verdict);

            Assert.Equal(verdict, incident.Verdict);
            Assert.NotNull(incident.ResolvedAt);

            var resolved = Assert.Single(Resolved(incident));

            Assert.Equal(incident.Id, resolved.IncidentId);
            Assert.Equal(closed, resolved.Status);
            Assert.Equal(verdict, resolved.Verdict);
            Assert.Equal(incident.ResolvedAt, resolved.ResolvedAt);
        }

        [Fact]
        public void ResolvedToClosedKeepsTheVerdictAndSaysNothingNew()
        {
            var incident = Create();
            incident.UpdateStatus(IncidentStatus.Resolved, IncidentVerdict.FalsePositive);
            var resolvedAt = incident.ResolvedAt;

            incident.UpdateStatus(IncidentStatus.Closed);

            Assert.Equal(IncidentStatus.Closed, incident.Status);
            Assert.Equal(IncidentVerdict.FalsePositive, incident.Verdict);
            Assert.Equal(resolvedAt, incident.ResolvedAt);
            Assert.Single(Resolved(incident));
        }

        [Fact]
        public void AVerdictCannotBeChangedOnceGiven()
        {
            var incident = Create();
            incident.UpdateStatus(IncidentStatus.Resolved, IncidentVerdict.Real);

            Assert.NotNull(incident.StatusChangeProblem(IncidentStatus.Closed, IncidentVerdict.FalsePositive));
            Assert.Throws<InvalidOperationException>(
                () => incident.UpdateStatus(IncidentStatus.Closed, IncidentVerdict.FalsePositive)
            );
            Assert.Equal(IncidentVerdict.Real, incident.Verdict);
        }

        [Fact]
        public void AVerdictIsRefusedWhenNothingIsBeingClosed()
        {
            var incident = Create();

            Assert.Throws<InvalidOperationException>(
                () => incident.UpdateStatus(IncidentStatus.InProgress, IncidentVerdict.Real)
            );
            Assert.Null(incident.Verdict);
        }

        [Fact]
        public void ReopeningClearsTheConclusionAndClosingAgainAsksAgain()
        {
            var incident = Create();
            incident.UpdateStatus(IncidentStatus.Resolved, IncidentVerdict.FalsePositive);

            incident.UpdateStatus(IncidentStatus.Open);

            Assert.Null(incident.Verdict);
            Assert.Null(incident.ResolvedAt);
            Assert.NotNull(incident.StatusChangeProblem(IncidentStatus.Resolved, verdict: null));

            incident.UpdateStatus(IncidentStatus.Resolved, IncidentVerdict.Real);

            Assert.Equal(IncidentVerdict.Real, incident.Verdict);
            Assert.Equal(2, Resolved(incident).Count);
        }
    }
}
