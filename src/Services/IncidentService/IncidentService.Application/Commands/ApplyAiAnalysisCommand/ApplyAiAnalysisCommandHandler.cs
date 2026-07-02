using IncidentService.Application.Abstractions;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.ApplyAiAnalysis;

public sealed class ApplyAiAnalysisCommandHandler : IRequestHandler<ApplyAiAnalysisCommand>
{
    private readonly IIncidentRepository _repository;

    public ApplyAiAnalysisCommandHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ApplyAiAnalysisCommand request, CancellationToken cancellationToken)
    {
        var incident =
            await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new IncidentNotFoundException(request.IncidentId);

        if (!Enum.TryParse<IncidentPriority>(request.SuggestedPriority, out var priority))
        {
            priority = incident.Priority;
        }

        incident.ApplyAiAnalysis(priority, request.SuggestedCategory, request.Reasoning);

        _repository.Update(incident);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
