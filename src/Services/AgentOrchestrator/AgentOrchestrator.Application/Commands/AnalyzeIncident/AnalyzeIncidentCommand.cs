using MediatR;

namespace AgentOrchestrator.Application.Commands.AnalyzeIncident;

public sealed record AnalyzeIncidentCommand : IRequest<Guid>
{
    public required Guid IncidentId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
}
