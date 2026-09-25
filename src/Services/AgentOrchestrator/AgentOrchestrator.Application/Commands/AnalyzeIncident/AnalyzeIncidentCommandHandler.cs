using AgentOrchestrator.Application.Changes;
using BuildingBlocks.SharedKernel;
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
    private readonly IOrganizationContext _organization;
    private readonly IGitHubConnectionRepository _connections;

    public AnalyzeIncidentCommandHandler(
        IAiAnalyzer aiAnalyzer,
        IIncidentAnalysisRepository repository,
        ILogger<AnalyzeIncidentCommandHandler> logger,
        IOrganizationContext organization,
        IGitHubConnectionRepository connections
    )
    {
        _aiAnalyzer = aiAnalyzer;
        _repository = repository;
        _logger = logger;
        _organization = organization;
        _connections = connections;
    }

    public async Task<Guid> Handle(
        AnalyzeIncidentCommand request,
        CancellationToken cancellationToken
    )
    {
        // Set by the bus from IncidentDetectedEvent, or by the middleware from the claim when the
        // endpoint is called by hand. Read once and passed on, so the analyzer receives the
        // organisation as an argument rather than reaching for ambient state.
        var organizationId = _organization.Required;

        var analysis = IncidentAnalysis.Create(
            organizationId,
            request.IncidentId,
            request.Title,
            request.Description
        );

        await _repository.AddAsync(analysis, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        try
        {
            var code = await CodeFor(request, cancellationToken);

            var result = await _aiAnalyzer.AnalyzeAsync(
                organizationId,
                request.IncidentId,
                request.Title,
                request.Description,
                code,
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
        }
        catch (Exception ex)
        {
            analysis.MarkAsFailed(ex.Message);
            _repository.Update(analysis);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "AI analysis failed for incident {IncidentId}",
                request.IncidentId
            );
        }

        return analysis.Id;
    }

    /// <summary>
    /// The repository this incident's service lives in, if the organisation connected GitHub and
    /// mapped it. Read through the organisation filter, so it can only ever be this organisation's.
    /// </summary>
    private async Task<CodeContext?> CodeFor(AnalyzeIncidentCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connections.GetAsync(cancellationToken);
        var repository = connection?.RepositoryFor(request.Service);

        if (connection is null || repository is null)
            return null;

        _logger.LogInformation(
            "Incident {IncidentId} ({Service}) may read {Repository}.",
            request.IncidentId,
            request.Service ?? "no service",
            repository.FullName
        );

        return new CodeContext(repository, connection.Token, request.DetectedAt ?? DateTime.UtcNow);
    }
}
