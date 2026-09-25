using MediatR;

namespace AgentOrchestrator.Application.Commands.AnalyzeIncident;

public sealed record AnalyzeIncidentCommand : IRequest<Guid>
{
    public required Guid IncidentId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }

    // Which service is failing and since when, when a signal opened the incident. Together they
    // decide whether the analysis may read code, and which commits count as "just before".
    public string? Service { get; init; }
    public DateTime? DetectedAt { get; init; }
}
