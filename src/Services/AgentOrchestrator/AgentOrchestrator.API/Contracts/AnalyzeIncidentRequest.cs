namespace AgentOrchestrator.API.Contracts;

public sealed record AnalyzeIncidentRequest
{
    public required Guid IncidentId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
}
