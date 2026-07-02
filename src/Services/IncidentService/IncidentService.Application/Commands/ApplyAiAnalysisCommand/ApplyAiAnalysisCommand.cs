using MediatR;

namespace IncidentService.Application.Commands.ApplyAiAnalysis;

public sealed record ApplyAiAnalysisCommand : IRequest
{
    public required Guid IncidentId { get; init; }
    public required string SuggestedPriority { get; init; }
    public required string SuggestedCategory { get; init; }
    public required string Reasoning { get; init; }
}
