using IncidentService.API.Contracts;
using IncidentService.Application.Commands.CreateIncident;
using IncidentService.Application.Queries.GetIncidentById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IncidentService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IncidentsController : ControllerBase
{
    private readonly ISender _sender;

    public IncidentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateIncidentCommand
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Source = request.Source,
            AssignedTeam = request.AssignedTeam
        };

        var result = await _sender.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetIncidentByIdQuery(id), cancellationToken);

        return Ok(result);
    }
}