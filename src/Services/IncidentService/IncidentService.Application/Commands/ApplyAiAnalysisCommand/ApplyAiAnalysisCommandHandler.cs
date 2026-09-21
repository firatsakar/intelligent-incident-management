using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.ApplyAiAnalysis;

public sealed class ApplyAiAnalysisCommandHandler : IRequestHandler<ApplyAiAnalysisCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IRealtimeNotifier _realtime;

    public ApplyAiAnalysisCommandHandler(
        IIncidentRepository repository,
        IRealtimeNotifier realtime
    )
    {
        _repository = repository;
        _realtime = realtime;
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

        incident.ApplyAiAnalysis(
            priority,
            request.SuggestedCategory,
            request.Reasoning,
            request.Confidence
        );

        _repository.Update(incident);
        await _repository.SaveChangesAsync(cancellationToken);

        // The moment a demo is watching for: the priority the AI decided on, arriving on a screen
        // that is already open.
        await _realtime.IncidentChangedAsync(
            IncidentDto.FromDomain(incident),
            cancellationToken
        );
    }
}
