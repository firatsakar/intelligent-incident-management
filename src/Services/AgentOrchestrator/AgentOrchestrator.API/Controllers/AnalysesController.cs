using AgentOrchestrator.API.Contracts;
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
}
