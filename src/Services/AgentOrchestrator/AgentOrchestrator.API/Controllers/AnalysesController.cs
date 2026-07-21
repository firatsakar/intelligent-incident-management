using AgentOrchestrator.API.Contracts;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Commands.AnalyzeIncident;
using MediatR;
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

    [HttpGet("similar")]
    public async Task<IActionResult> FindSimilar(
        [FromServices] ISimilarAnalysisSearcher searcher,
        [FromQuery] string title,
        [FromQuery] string description,
        CancellationToken ct
    ) => Ok(await searcher.SearchAsync(title, description, cancellationToken: ct));

    [HttpPost("reindex")]
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
