using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Queries.GetIncidentDeliveries;
using NotificationService.Application.Queries.GetNotificationStats;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    // Delivery history for one incident: what was sent, to which integration, and why it failed.
    [HttpGet("incident/{incidentId:guid}")]
    public async Task<IActionResult> GetByIncident(
        Guid incidentId,
        CancellationToken cancellationToken
    )
    {
        return Ok(
            await _sender.Send(new GetIncidentDeliveriesQuery(incidentId), cancellationToken)
        );
    }

    // Delivery health per integration. The endpoint above answers "did anyone hear about this
    // incident"; this one answers "is this channel working", which nothing could answer before.
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default
    )
    {
        if (from.HasValue && to.HasValue && from >= to)
            return BadRequest(new { error = "'from' must be earlier than 'to'." });

        return Ok(await _sender.Send(new GetNotificationStatsQuery(from, to), cancellationToken));
    }
}
