using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Queries.GetIncidentDeliveries;

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
}
