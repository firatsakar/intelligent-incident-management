using BuildingBlocks.SharedKernel;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Events;

namespace IncidentService.Domain.Aggregates;

public sealed class Incident : AggregateRoot
{
    private Incident() { }

    /// <summary>
    /// The team this incident belongs to. Everything else about who may see it follows from this:
    /// an incident is shared by an organisation, not owned by whoever happened to open it.
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public IncidentStatus Status { get; private set; }
    public IncidentPriority Priority { get; private set; }
    public IncidentSource Source { get; private set; }
    public string? AssignedTeam { get; private set; }
    public string? AiSuggestedCategory { get; private set; }
    public string? AiReasoning { get; private set; }
    public bool IsAiAnalyzed { get; private set; }

    // How sure the analysis was, on its own calibration. IncidentAnalyzedEvent has carried this
    // since Adım 12 and nothing stored it, so the number the product leans on hardest could not be
    // shown anywhere. Nullable because an analysis may decline to give one.
    public double? AiConfidence { get; private set; }

    // Non-null means the analysis ran and failed. Distinct from IsAiAnalyzed being false, which
    // means it has not run yet — one of those resolves itself and the other does not.
    public string? AiAnalysisError { get; private set; }

    // When the problem started, as distinct from CreatedAt, which is when the record was filed.
    // An engineer opening an incident at 14:35 for something that began at 14:20 has the same
    // need as a telemetry promotion: correlating evidence against the wrong moment finds nothing.
    public DateTime? DetectedAt { get; private set; }

    public static Incident Create(
        Guid organizationId,
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
            OrganizationId = organizationId,
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

    // No default on confidence: every caller already knows whether the analysis gave one, and a
    // default would let a new call site drop it silently — which is exactly how this field came to
    // be missing in the first place.
    public void ApplyAiAnalysis(
        IncidentPriority suggestedPriority,
        string suggestedCategory,
        string reasoning,
        double? confidence
    )
    {
        Priority = suggestedPriority;
        AiSuggestedCategory = suggestedCategory;
        AiReasoning = reasoning;
        AiConfidence = confidence;
        IsAiAnalyzed = true;

        // A late success clears an earlier failure. Analysis is retried through redelivery, so an
        // incident that failed once and then succeeded must not keep wearing the failure.
        AiAnalysisError = null;

        SetUpdatedAt();
    }

    /// <summary>
    /// The analysis was attempted and produced nothing.
    ///
    /// This is not the same as <see cref="IsAiAnalyzed"/> being false, and that distinction is
    /// the whole reason the field exists: "not analysed yet" is a state that resolves itself,
    /// and "analysis failed" is one that does not. Until this was recorded, the two were
    /// indistinguishable on screen and a failed incident waited for an enrichment that was never
    /// coming.
    ///
    /// <see cref="IsAiAnalyzed"/> stays false, because no analysis was applied — the flag means
    /// "these AI fields hold something", and here they do not.
    /// </summary>
    public void RecordAiAnalysisFailure(string error)
    {
        // Idempotent for the same reason ApplyAiAnalysis is: delivery is at-least-once, and a
        // redelivered failure must not look like a second, different one.
        AiAnalysisError = error;
        SetUpdatedAt();
    }
}
