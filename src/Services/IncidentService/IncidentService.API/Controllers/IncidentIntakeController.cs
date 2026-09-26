using BuildingBlocks.SharedKernel;
using IncidentService.API.Contracts;
using IncidentService.Application.Abstractions;
using IncidentService.Application.Commands.IntakeIncident;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentService.API.Controllers;

/// <summary>
/// Where an external system opens an incident with one of the organisation's API keys (Adım 27).
/// </summary>
/// <remarks>
/// <para>
/// Nobody signs in to send an alert, so the platform's token is not what authenticates this: the
/// key is — and the key is also the only thing that says whose incident it is, which is why this
/// endpoint, and nothing after it, sets the organisation. The same rule as the OTLP receiver, and
/// the same kind of header: its own, not <c>Authorization</c>, so it is never mistaken for a
/// platform token.
/// </para>
/// <para>
/// 401 for a missing or unknown key is final; senders do not retry it into a storm. The gateway
/// limits each key to a rate no human-scale alerting reaches.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/incidents/intake")]
public sealed class IncidentIntakeController : ControllerBase
{
    public const string ApiKeyHeader = "X-IIM-Api-Key";

    private readonly IIncidentApiKeyRepository _keys;
    private readonly IOrganizationContext _organization;
    private readonly ISender _sender;

    public IncidentIntakeController(IIncidentApiKeyRepository keys, IOrganizationContext organization, ISender sender)
    {
        _keys = keys;
        _organization = organization;
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Intake([FromBody] IncidentIntakeRequest request, CancellationToken cancellationToken)
    {
        var presented = Request.Headers[ApiKeyHeader].ToString().Trim();

        if (presented.Length == 0)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: $"Missing {ApiKeyHeader} header.");

        var key = await _keys.FindByHashForIntakeAsync(AccessKey.Hash(presented), cancellationToken);

        if (key is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unknown API key.");

        // Everything from here on belongs to the key's organisation, and to nobody else's.
        _organization.Set(key.OrganizationId);

        // Saved on its own: a request that turns out invalid, or matches an open incident, still
        // used the key.
        if (key.MarkUsed(DateTime.UtcNow))
            await _keys.SaveChangesAsync(cancellationToken);

        var result = await _sender.Send(
            new IntakeIncidentCommand
            {
                Title = request.Title ?? string.Empty,
                Description = request.Description ?? string.Empty,
                Priority = request.Priority,
                ExternalId = request.ExternalId,
                DetectedAt = request.DetectedAt,
                KeyName = key.Name,
            },
            cancellationToken
        );

        return result.Created ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
    }
}
