using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Aggregates;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Application.Commands.AnalyzeIncident;

public sealed class AnalyzeIncidentCommandHandler : IRequestHandler<AnalyzeIncidentCommand, Guid>
{
    private readonly IAiAnalyzer _aiAnalyzer;
    private readonly IIncidentAnalysisRepository _repository;
    private readonly ILogger<AnalyzeIncidentCommandHandler> _logger;
    private readonly IEventBus _eventBus;

    public AnalyzeIncidentCommandHandler(
        IAiAnalyzer aiAnalyzer,
        IIncidentAnalysisRepository repository,
        ILogger<AnalyzeIncidentCommandHandler> logger,
        IEventBus eventBus
    )
    {
        _aiAnalyzer = aiAnalyzer;
        _repository = repository;
        _logger = logger;
        _eventBus = eventBus;
    }

    public async Task<Guid> Handle(
        AnalyzeIncidentCommand request,
        CancellationToken cancellationToken
    )
    {
        var analysis = IncidentAnalysis.Create(
            request.IncidentId,
            request.Title,
            request.Description
        );

        await _repository.AddAsync(analysis, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await _aiAnalyzer.AnalyzeIncidentAsync(
                request.Title,
                request.Description,
                cancellationToken
            );

            analysis.MarkAsCompleted(result);

            _repository.Update(analysis);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Incident {IncidentId} analyzed: Priority={Priority}, Category={Category}",
                request.IncidentId,
                result.SuggestedPriority,
                result.SuggestedCategory
            );

            await _eventBus.PublishAsync(
                new IncidentAnalyzedEvent
                {
                    IncidentId = request.IncidentId,
                    SuggestedPriority = result.SuggestedPriority,
                    SuggestedCategory = result.SuggestedCategory,
                    Reasoning = result.Reasoning,
                },
                cancellationToken
            );

            _logger.LogInformation(
                "IncidentAnalyzedEvent published for incident {IncidentId}.",
                request.IncidentId
            );
        }
        catch (Exception ex)
        {
            analysis.MarkAsFailed(ex.Message);

            _logger.LogError(
                ex,
                "AI analysis failed for incident {IncidentId}",
                request.IncidentId
            );
        }

        return analysis.Id;
    }
}
