using IncidentService.Domain.ValueObjects;
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

    // Commits the analysis named as likely causes (Adım 17.5). Replaced by each analysis; empty
    // when it read no code or found nothing that explained the incident.
    private List<AiRelatedChange> _aiRelatedChanges = [];

    public IReadOnlyList<AiRelatedChange> AiRelatedChanges => _aiRelatedChanges;

    // When the problem started, as distinct from CreatedAt, which is when the record was filed.
    // An engineer opening an incident at 14:35 for something that began at 14:20 has the same
    // need as a telemetry promotion: correlating evidence against the wrong moment finds nothing.
    public DateTime? DetectedAt { get; private set; }

    // What the people who worked it concluded: a real problem, or a detection that should not have
    // fired. Asked once, on the way from open to closed, because that is the moment somebody knows
    // — and it is what telemetry learns from. Null while open, and for incidents closed before
    // anyone was asked.
    public IncidentVerdict? Verdict { get; private set; }

    // When it was closed out. Null while open; cleared when reopened.
    public DateTime? ResolvedAt { get; private set; }

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
        // No domain event. There was one — IncidentCreatedDomainEvent — and nothing ever listened:
        // the service published IncidentDetectedEvent itself after saving. Once the outbox arrived
        // (Adım 24) an event nobody handles would have become a row retried forever, so it went.
        return new Incident
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
    }

    public static bool IsClosed(IncidentStatus status) =>
        status is IncidentStatus.Resolved or IncidentStatus.Closed;

    /// <summary>
    /// Why this change cannot be made, or null when it can. Public so the application layer can
    /// refuse it as a validation failure before the aggregate is asked to do it.
    /// </summary>
    public string? StatusChangeProblem(IncidentStatus newStatus, IncidentVerdict? verdict)
    {
        var closing = !IsClosed(Status) && IsClosed(newStatus);

        if (closing && verdict is null)
            return "Closing an incident needs a verdict: was it a real problem or a false positive?";

        // Changing a verdict after the fact would have to un-teach telemetry what it already
        // learned from it, which nothing does yet. Refused rather than quietly ignored.
        if (!closing && verdict is not null)
            return "A verdict is given once, when an open incident is closed.";

        return null;
    }

    public void UpdateStatus(IncidentStatus newStatus, IncidentVerdict? verdict = null)
    {
        if (StatusChangeProblem(newStatus, verdict) is { } problem)
            throw new InvalidOperationException(problem);

        var wasClosed = IsClosed(Status);
        var nowClosed = IsClosed(newStatus);

        Status = newStatus;

        if (!wasClosed && nowClosed)
        {
            var resolvedAt = DateTime.UtcNow;

            Verdict = verdict;
            ResolvedAt = resolvedAt;

            AddDomainEvent(
                new IncidentResolvedDomainEvent
                {
                    IncidentId = Id,
                    Status = newStatus,
                    Verdict = verdict!.Value,
                    ResolvedAt = resolvedAt,
                }
            );
        }
        else if (wasClosed && !nowClosed)
        {
            // Reopened: whatever was concluded no longer stands. What telemetry learned from it is
            // not taken back — see IncidentResolvedDomainEvent.
            Verdict = null;
            ResolvedAt = null;
        }

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
        double? confidence,
        IReadOnlyList<AiRelatedChange> relatedChanges
    )
    {
        Priority = suggestedPriority;
        _aiRelatedChanges = relatedChanges.Where(change => AiRelatedChange.IsSafeUrl(change.Url)).Take(5).ToList();
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
