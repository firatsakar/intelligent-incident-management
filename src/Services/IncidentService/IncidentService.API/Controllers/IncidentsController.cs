using IncidentService.API.Contracts;
using IncidentService.Application.Commands.AssignTeam;
using IncidentService.Application.Commands.CreateIncident;
using IncidentService.Application.Commands.UpdateIncidentStatus;
using IncidentService.Application.Queries.GetIncidentById;
using IncidentService.Application.Queries.GetIncidents;
using IncidentService.Domain.Enums;
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
        CancellationToken cancellationToken
    )
    {
        var command = new CreateIncidentCommand
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Source = request.Source,
            AssignedTeam = request.AssignedTeam,
        };

        var result = await _sender.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetIncidentByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] IncidentStatus? status,
        [FromQuery] IncidentPriority? priority,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default
    )
    {
        var query = new GetIncidentsQuery
        {
            Status = status,
            Priority = priority,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new UpdateIncidentStatusCommand
        {
            IncidentId = id,
            NewStatus = request.NewStatus,
        };

        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/team")]
    public async Task<IActionResult> AssignTeam(
        Guid id,
        [FromBody] AssignTeamRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new AssignTeamCommand { IncidentId = id, Team = request.Team };

        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}
