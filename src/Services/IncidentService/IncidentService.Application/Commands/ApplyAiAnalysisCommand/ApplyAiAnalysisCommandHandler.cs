using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.ApplyAiAnalysis;

public sealed class ApplyAiAnalysisCommandHandler : IRequestHandler<ApplyAiAnalysisCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IRealtimeNotifier _realtime;
    private readonly IIncidentActivityRepository _activity;

    public ApplyAiAnalysisCommandHandler(
        IIncidentRepository repository,
        IRealtimeNotifier realtime,
        IIncidentActivityRepository activity
    )
    {
        _repository = repository;
        _realtime = realtime;
        _activity = activity;
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

        var firstAnalysis = !incident.IsAiAnalyzed;
        var priorityBefore = incident.Priority;

        incident.ApplyAiAnalysis(
            priority,
            request.SuggestedCategory,
            request.Reasoning,
            request.Confidence,
            request.RelatedChanges
        );

        // Delivery is at-least-once: a redelivered result is applied again, harmlessly, and must
        // not appear in the trail twice.
        var recorded = firstAnalysis ? IncidentActivity.AnalysisApplied(incident, priorityBefore) : null;

        if (recorded is not null)
            _activity.Add(recorded);

        _repository.Update(incident);
        await _repository.SaveChangesAsync(cancellationToken);

        // The moment a demo is watching for: the priority the AI decided on, arriving on a screen
        // that is already open.
        await _realtime.IncidentChangedAsync(
            IncidentDto.FromDomain(incident),
            cancellationToken
        );
        await _realtime.ActivityRecordedAsync(recorded, cancellationToken);
    }
}
