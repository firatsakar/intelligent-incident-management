using AgentOrchestrator.Domain.Enums;
using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Application.DTOs;

public sealed record IncidentAnalysisDto
{
    public Guid Id { get; init; }
    public Guid IncidentId { get; init; }
    public string IncidentTitle { get; init; } = default!;
    public AnalysisStatus Status { get; init; }
    public AnalysisResult? Result { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
