using IncidentService.Domain.ValueObjects;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;

namespace IncidentService.Application.DTOs;

public sealed record IncidentDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = default!;
    public string Description { get; init; } = default!;
    public IncidentStatus Status { get; init; }
    public IncidentPriority Priority { get; init; }
    public IncidentSource Source { get; init; }
    public string? AssignedTeam { get; init; }

    // When the problem started, as opposed to CreatedAt, which is when the record was filed.
    public DateTime? DetectedAt { get; init; }

    // The analysis results were populated in the database but absent from every response, so the
    // AI's work was invisible over HTTP — the gap noted during Adım 12.
    public string? AiSuggestedCategory { get; init; }
    public string? AiReasoning { get; init; }
    public bool IsAiAnalyzed { get; init; }
    public double? AiConfidence { get; init; }

    /// <summary>
    /// Non-null means the analysis ran and failed, which is a different screen state from
    /// IsAiAnalyzed being false. One of those resolves itself and the other does not.
    /// </summary>
    public string? AiAnalysisError { get; init; }

    // Commits the analysis named as likely causes, with links the platform built (Adım 17.5).
    public IReadOnlyList<AiRelatedChange> AiRelatedChanges { get; init; } = [];

    // The conclusion reached when it was closed, and when. Both null while open.
    public IncidentVerdict? Verdict { get; init; }
    public DateTime? ResolvedAt { get; init; }

    // Only for an incident opened through the incident API (Adım 27).
    public string? ExternalId { get; init; }
    public string? ReportedBy { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    // One mapping rather than one per call site. Three hand-written copies are how the AI fields
    // came to be missing from every response in the first place.
    public static IncidentDto FromDomain(Incident incident)
    {
        return new IncidentDto
        {
            Id = incident.Id,
            Title = incident.Title,
            Description = incident.Description,
            Status = incident.Status,
            Priority = incident.Priority,
            Source = incident.Source,
            AssignedTeam = incident.AssignedTeam,
            DetectedAt = incident.DetectedAt,
            AiSuggestedCategory = incident.AiSuggestedCategory,
            AiReasoning = incident.AiReasoning,
            IsAiAnalyzed = incident.IsAiAnalyzed,
            AiConfidence = incident.AiConfidence,
            AiAnalysisError = incident.AiAnalysisError,
            AiRelatedChanges = incident.AiRelatedChanges,
            Verdict = incident.Verdict,
            ResolvedAt = incident.ResolvedAt,
            ExternalId = incident.ExternalId,
            ReportedBy = incident.ReportedBy,
            CreatedAt = incident.CreatedAt,
            UpdatedAt = incident.UpdatedAt,
        };
    }
}
