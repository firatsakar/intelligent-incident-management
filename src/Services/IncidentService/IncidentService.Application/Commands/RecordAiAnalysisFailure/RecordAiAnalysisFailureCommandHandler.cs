using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.RecordAiAnalysisFailure;

/// <summary>
/// The failure counterpart of <see cref="ApplyAiAnalysis.ApplyAiAnalysisCommandHandler"/>, and it
/// reuses the same push: an incident that failed analysis is an incident that changed, so
/// <c>incidentChanged</c> carries it and no new hub or message type is needed.
/// </summary>
public sealed class RecordAiAnalysisFailureCommandHandler
    : IRequestHandler<RecordAiAnalysisFailureCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IRealtimeNotifier _realtime;
    private readonly IIncidentActivityRepository _activity;

    public RecordAiAnalysisFailureCommandHandler(
        IIncidentRepository repository,
        IRealtimeNotifier realtime,
        IIncidentActivityRepository activity
    )
    {
        _repository = repository;
        _realtime = realtime;
        _activity = activity;
    }

    public async Task Handle(
        RecordAiAnalysisFailureCommand request,
        CancellationToken cancellationToken
    )
    {
        var incident =
            await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new IncidentNotFoundException(request.IncidentId);

        // A later success clears this, and the aggregate owns that rule. Nothing here decides
        // whether a failure is still current — redelivery would make that decision wrong.
        var errorBefore = incident.AiAnalysisError;

        incident.RecordAiAnalysisFailure(request.Error);

        // The same failure redelivered is the same failure, recorded once.
        if (incident.AiAnalysisError != errorBefore)
            _activity.Add(IncidentActivity.AnalysisFailed(incident));

        _repository.Update(incident);
        await _repository.SaveChangesAsync(cancellationToken);

        await _realtime.IncidentChangedAsync(
            IncidentDto.FromDomain(incident),
            cancellationToken
        );
    }
}
