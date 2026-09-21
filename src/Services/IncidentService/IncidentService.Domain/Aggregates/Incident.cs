using BuildingBlocks.SharedKernel;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Events;

namespace IncidentService.Domain.Aggregates;

public sealed class Incident : AggregateRoot
{
    private Incident() { }

    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public IncidentStatus Status { get; private set; }
    public IncidentPriority Priority { get; private set; }
    public IncidentSource Source { get; private set; }
    public string? AssignedTeam { get; private set; }
    public string? AiSuggestedCategory { get; private set; }
    public string? AiReasoning { get; private set; }
    public bool IsAiAnalyzed { get; private set; }

    // When the problem started, as distinct from CreatedAt, which is when the record was filed.
    // An engineer opening an incident at 14:35 for something that began at 14:20 has the same
    // need as a telemetry promotion: correlating evidence against the wrong moment finds nothing.
    public DateTime? DetectedAt { get; private set; }

    public static Incident Create(
        string title,
        string description,
        IncidentPriority priority,
        IncidentSource source,
        string? assignedTeam = null,
        // Supplied when the caller already knows the identity — a telemetry promotion picks the
        // id so that a redelivered event collides on the primary key instead of opening a second
        // incident.
        Guid? id = null,
        DateTime? detectedAt = null
    )
    {
        var incident = new Incident
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = IncidentStatus.Open,
            Priority = priority,
            Source = source,
            AssignedTeam = assignedTeam,
            DetectedAt = detectedAt,
        };

        incident.AddDomainEvent(
            new IncidentCreatedDomainEvent { IncidentId = incident.Id, Title = incident.Title }
        );

        return incident;
    }

    public void UpdateStatus(IncidentStatus newStatus)
    {
        Status = newStatus;
        SetUpdatedAt();
    }

    public void AssignTeam(string team)
    {
        AssignedTeam = team;
        SetUpdatedAt();
    }

    public void ApplyAiAnalysis(
        IncidentPriority suggestedPriority,
        string suggestedCategory,
        string reasoning
    )
    {
        Priority = suggestedPriority;
        AiSuggestedCategory = suggestedCategory;
        AiReasoning = reasoning;
        IsAiAnalyzed = true;
        SetUpdatedAt();
    }
}
