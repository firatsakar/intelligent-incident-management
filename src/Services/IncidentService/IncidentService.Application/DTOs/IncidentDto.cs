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
            CreatedAt = incident.CreatedAt,
            UpdatedAt = incident.UpdatedAt,
        };
    }
}
