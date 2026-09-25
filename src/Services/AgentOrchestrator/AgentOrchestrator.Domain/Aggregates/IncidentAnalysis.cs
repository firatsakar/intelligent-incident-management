using AgentOrchestrator.Domain.Enums;
using AgentOrchestrator.Domain.Events;
using AgentOrchestrator.Domain.ValueObjects;
using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.Aggregates;

public sealed class IncidentAnalysis : AggregateRoot
{
    /// <summary>
    /// Whose incident this analysis is about. Carried here as well as on the incident because the
    /// two live in different databases, and because it is also what keeps one organisation's past
    /// analyses out of the search the model runs for another's.
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public Guid IncidentId { get; private set; }
    public string IncidentTitle { get; private set; } = default!;
    public string IncidentDescription { get; private set; } = default!;

    public AnalysisStatus Status { get; private set; }

    public AnalysisResult? Result { get; private set; }

    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private IncidentAnalysis() { }

    public static IncidentAnalysis Create(
        Guid organizationId,
        Guid incidentId,
        string incidentTitle,
        string incidentDescription
    )
    {
        return new IncidentAnalysis
        {
            OrganizationId = organizationId,
            IncidentId = incidentId,
            IncidentTitle = incidentTitle,
            IncidentDescription = incidentDescription,
            Status = AnalysisStatus.Pending,
        };
    }

    public void MarkAsCompleted(AnalysisResult result)
    {
        Result = result;
        Status = AnalysisStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(
            new IncidentAnalysisCompletedDomainEvent(
                IncidentId,
                IncidentTitle,
                IncidentDescription,
                result.SuggestedCategory,
                result.SuggestedPriority,
                result.Reasoning,
                result.Confidence,
                result.RelatedChanges
            )
        );

        SetUpdatedAt();
    }

    public void MarkAsFailed(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = AnalysisStatus.Failed;

        // Failure used to end here, which made it the one outcome that never left this service:
        // no domain event, so no outbox row, so no write-back, so no push — and an incident that
        // said "analysis pending" for the rest of its life. The state that most needs reporting
        // was the only one that never was.
        AddDomainEvent(
            new IncidentAnalysisFailedDomainEvent(IncidentId, IncidentTitle, errorMessage)
        );

        SetUpdatedAt();
    }
}
