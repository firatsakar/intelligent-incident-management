using IncidentService.API.Contracts;
using IncidentService.Application.Commands.AssignTeam;
using IncidentService.Application.Commands.CreateIncident;
using IncidentService.Application.Commands.UpdateIncidentStatus;
using IncidentService.Application.Queries.GetIncidentById;
using IncidentService.Application.Queries.GetIncidents;
using IncidentService.Application.Queries.GetIncidentStats;
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
            DetectedAt = request.DetectedAt,
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

    /// <summary>
    /// Arrival shape over time plus the current open picture, for the dashboard. The guid
    /// constraint on GetById is what keeps "stats" from being read as an id.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default
    )
    {
        if (from.HasValue && to.HasValue && from >= to)
            return BadRequest(new { error = "'from' must be earlier than 'to'." });

        var result = await _sender.Send(new GetIncidentStatsQuery(from, to), cancellationToken);

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
