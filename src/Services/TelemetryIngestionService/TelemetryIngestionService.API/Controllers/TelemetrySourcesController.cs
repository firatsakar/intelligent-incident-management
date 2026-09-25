using Microsoft.AspNetCore.Authorization;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TelemetryIngestionService.API.Contracts;
using TelemetryIngestionService.Application.Commands.CreateTelemetrySource;
using TelemetryIngestionService.Application.Commands.DeleteTelemetrySource;
using TelemetryIngestionService.Application.Commands.RotateIngestKey;
using TelemetryIngestionService.Application.Commands.SetTelemetrySourceEnabled;
using TelemetryIngestionService.Application.Commands.TestTelemetrySource;
using TelemetryIngestionService.Application.Commands.UpdateTelemetrySource;
using TelemetryIngestionService.Application.Queries.GetTelemetrySourceById;
using TelemetryIngestionService.Application.Queries.GetTelemetrySources;

namespace TelemetryIngestionService.API.Controllers;

[ApiController]
[Route("api/telemetry-sources")]
// The organisation's configuration, reads included: only its Admins see where alerts go and which
// logs are read (Adım 16.5). On the class so an action added later cannot forget it.
[Authorize(Policy = PlatformPolicies.Administer)]
public sealed class TelemetrySourcesController : ControllerBase
{
    private readonly ISender _sender;

    public TelemetrySourcesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetTelemetrySourcesQuery(), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetTelemetrySourceByIdQuery(id), cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTelemetrySourceRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new CreateTelemetrySourceCommand
        {
            Name = request.Name,
            Kind = request.Kind,
            Config = request.Config,
            PollIntervalSeconds = request.PollIntervalSeconds,
            IsEnabled = request.IsEnabled,
        };

        var result = await _sender.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTelemetrySourceRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new UpdateTelemetrySourceCommand
        {
            Id = id,
            Name = request.Name,
            Config = request.Config,
            PollIntervalSeconds = request.PollIntervalSeconds,
        };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    [HttpPatch("{id:guid}/enabled")]
    public async Task<IActionResult> SetEnabled(
        Guid id,
        [FromBody] SetTelemetrySourceEnabledRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new SetTelemetrySourceEnabledCommand(id, request.IsEnabled);

        return Ok(await _sender.Send(command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteTelemetrySourceCommand(id), cancellationToken);

        return NoContent();
    }

    // The key is in this response and nowhere else, ever.
    [HttpPost("{id:guid}/rotate-key")]
    public async Task<IActionResult> RotateKey(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new RotateIngestKeyCommand(id), cancellationToken));
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new TestTelemetrySourceCommand(id), cancellationToken);

        // A failure here is the external system or its credentials, not a bad request to us.
        return result.IsSuccess ? Ok(result) : StatusCode(StatusCodes.Status502BadGateway, result);
    }
}
