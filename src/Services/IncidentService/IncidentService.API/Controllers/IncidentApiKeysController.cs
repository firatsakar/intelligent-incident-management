using BuildingBlocks.Web;
using IncidentService.Application.Commands.CreateIncidentApiKey;
using IncidentService.Application.Commands.DeleteIncidentApiKey;
using IncidentService.Application.Queries.ListIncidentApiKeys;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentService.API.Controllers;

/// <summary>
/// The organisation's incident API keys (Adım 27). Admin only, reads included, like every other
/// organisation setting (Adım 16.5).
/// </summary>
[ApiController]
[Route("api/incident-api-keys")]
[Authorize(Policy = PlatformPolicies.Administer)]
public sealed class IncidentApiKeysController : ControllerBase
{
    private readonly ISender _sender;

    public IncidentApiKeysController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new ListIncidentApiKeysQuery(), cancellationToken));

    /// <summary>The response is the only place the key's value ever appears.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIncidentApiKeyRequest request, CancellationToken cancellationToken)
    {
        var issued = await _sender.Send(
            new CreateIncidentApiKeyCommand(request.Name ?? string.Empty, User.Identity?.Name ?? "Admin"),
            cancellationToken
        );

        return StatusCode(StatusCodes.Status201Created, issued);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteIncidentApiKeyCommand(id), cancellationToken);

        return NoContent();
    }

    public sealed record CreateIncidentApiKeyRequest(string? Name);
}
