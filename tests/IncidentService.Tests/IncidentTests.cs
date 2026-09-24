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
    public void Create_OpensTheIncidentAndAnnouncesIt()
    {
        var incident = Create();

        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.False(incident.IsAiAnalyzed);

        var created = Assert.Single(incident.DomainEvents.OfType<IncidentCreatedDomainEvent>());

        Assert.Equal(incident.Id, created.IncidentId);
        Assert.Equal(incident.Title, created.Title);
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

        incident.UpdateStatus(IncidentStatus.Resolved);
        incident.AssignTeam("payments");

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal("payments", incident.AssignedTeam);
        Assert.NotNull(incident.UpdatedAt);
    }
}
