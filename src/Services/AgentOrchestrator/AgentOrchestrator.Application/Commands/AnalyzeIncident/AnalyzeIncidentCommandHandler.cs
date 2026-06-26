using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Aggregates;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Application.Commands.AnalyzeIncident;

public sealed class AnalyzeIncidentCommandHandler : IRequestHandler<AnalyzeIncidentCommand, Guid>
{
    private readonly IAiAnalyzer _aiAnalyzer;
    private readonly IIncidentAnalysisRepository _repository;
    private readonly ILogger<AnalyzeIncidentCommandHandler> _logger;

    public AnalyzeIncidentCommandHandler(
        IAiAnalyzer aiAnalyzer,
        IIncidentAnalysisRepository repository,
        ILogger<AnalyzeIncidentCommandHandler> logger
    )
    {
        _aiAnalyzer = aiAnalyzer;
        _repository = repository;
        _logger = logger;
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

            _logger.LogInformation(
                "Incident {IncidentId} analyzed: Priority={Priority}, Category={Category}",
                request.IncidentId,
                result.SuggestedPriority,
                result.SuggestedCategory
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

        _repository.Update(analysis);
        await _repository.SaveChangesAsync(cancellationToken);

        return analysis.Id;
    }
}
