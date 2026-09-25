using MediatR;

namespace IncidentService.Application.Commands.RecordAiAnalysisFailure;

public sealed record RecordAiAnalysisFailureCommand : IRequest
{
    public required Guid IncidentId { get; init; }
    public required string Error { get; init; }
}
