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

    [HttpPost("reindex")]
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
        var analyses = await repository.GetCompletedAsync(cancellationToken);
        await indexer.IndexManyAsync(analyses, cancellationToken);
        return Ok(new { Reindexed = analyses.Count });
    }
}
