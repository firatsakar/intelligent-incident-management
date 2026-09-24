using Microsoft.AspNetCore.Authorization;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationService.API.Contracts;
using NotificationService.Application.Commands.CreateIntegration;
using NotificationService.Application.Commands.DeleteIntegration;
using NotificationService.Application.Commands.SendTestNotification;
using NotificationService.Application.Commands.SetIntegrationEnabled;
using NotificationService.Application.Commands.UpdateIntegration;
using NotificationService.Application.Queries.GetIntegrationById;
using NotificationService.Application.Queries.GetIntegrations;

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

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetIntegrationsQuery(), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetIntegrationByIdQuery(id), cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = PlatformPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateIntegrationRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new CreateIntegrationCommand
        {
            Name = request.Name,
            Channel = request.Channel,
            Config = request.Config,
            MinPriority = request.MinPriority,
            CategoryFilter = request.CategoryFilter,
            IsEnabled = request.IsEnabled,
        };

        var result = await _sender.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PlatformPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateIntegrationRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new UpdateIntegrationCommand
        {
            Id = id,
            Name = request.Name,
            Config = request.Config,
            MinPriority = request.MinPriority,
            CategoryFilter = request.CategoryFilter,
        };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    [HttpPatch("{id:guid}/enabled")]
    [Authorize(Policy = PlatformPolicies.Operate)]
    public async Task<IActionResult> SetEnabled(
        Guid id,
        [FromBody] SetIntegrationEnabledRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new SetIntegrationEnabledCommand(id, request.IsEnabled);

        return Ok(await _sender.Send(command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PlatformPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteIntegrationCommand(id), cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    [Authorize(Policy = PlatformPolicies.Operate)]
    public async Task<IActionResult> SendTest(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SendTestNotificationCommand(id), cancellationToken);

        // A failure here is the customer's endpoint or credentials, not a bad request to us.
        return result.IsSuccess ? Ok(result) : StatusCode(StatusCodes.Status502BadGateway, result);
    }
}
