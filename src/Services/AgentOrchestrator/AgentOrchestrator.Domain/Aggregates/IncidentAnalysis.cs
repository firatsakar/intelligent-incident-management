using AgentOrchestrator.Domain.Enums;
using AgentOrchestrator.Domain.ValueObjects;
using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.Aggregates;

public sealed class IncidentAnalysis : AggregateRoot
{
    public Guid IncidentId { get; private set; }
    public string IncidentTitle { get; private set; } = default!;
    public string IncidentDescription { get; private set; } = default!;

    public AnalysisStatus Status { get; private set; }

    public AnalysisResult? Result { get; private set; }

    public string? ErrorMessage { get; private set; }

    private IncidentAnalysis() { }

    public static IncidentAnalysis Create(
        Guid incidentId,
        string incidentTitle,
        string incidentDescription
    )
    {
        return new IncidentAnalysis
        {
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
        SetUpdatedAt();
    }

    public void MarkAsFailed(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = AnalysisStatus.Failed;
        SetUpdatedAt();
    }
}
