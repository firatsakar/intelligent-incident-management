using BuildingBlocks.Web;
using AgentOrchestrator.API.Contracts;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Commands.AnalyzeIncident;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AnalysesController : ControllerBase
{
    private readonly ISender _sender;

    public AnalysesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Authorize(Policy = PlatformPolicies.Operate)]
    public async Task<IActionResult> Analyze(
        [FromBody] AnalyzeIncidentRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new AnalyzeIncidentCommand
        {
            IncidentId = request.IncidentId,
            Title = request.Title,
            Description = request.Description,
        };

        var analysisId = await _sender.Send(command, cancellationToken);

        return Ok(new { analysisId });
    }

    // Absolute, and outside /api on purpose: the gateway routes /api, /hubs and /otlp and nothing
    // else, so this is reachable only from inside, against the service itself. Until Adım 16.5 it
    // was /api/analyses/reindex — anonymous and routed, so anyone on the internet could make the
    // platform rebuild every organisation's index. Not an Admin endpoint either: an organisation's
    // Admin has no business rebuilding all of them.
    [HttpPost("/internal/analyses/reindex")]
    [ApiExplorerSettings(IgnoreApi = true)]
    // An operational rebuild of the Elasticsearch index, run by whoever changed a mapping. There
    // is no user behind it and therefore no token it could carry. It reads and rewrites the search
    // view of what the database already holds; it creates nothing and decides nothing.
    [AllowAnonymous]
    public async Task<IActionResult> Reindex(
        [FromServices] IIncidentAnalysisRepository repository,
        [FromServices] IAnalysisIndexer indexer,
        CancellationToken cancellationToken
    )
    {
        var analyses = await repository.GetAllCompletedForReindexAsync(cancellationToken);
        await indexer.IndexManyAsync(analyses, cancellationToken);
        return Ok(new { Reindexed = analyses.Count });
    }
}
