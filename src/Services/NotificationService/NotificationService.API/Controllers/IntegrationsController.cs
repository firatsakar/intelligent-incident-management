using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Commands.SendTestNotification;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IntegrationsController : ControllerBase
{
    private readonly ISender _sender;

    public IntegrationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> SendTest(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SendTestNotificationCommand(id), cancellationToken);

        if (result is null)
            return NotFound();

        return result.IsSuccess ? Ok(result) : StatusCode(StatusCodes.Status502BadGateway, result);
    }
}
