using AgentOrchestrator.Application.Commands.SaveAiSettings;
using AgentOrchestrator.Application.Queries.GetAiSettings;
using AgentOrchestrator.Domain.Enums;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.API.Controllers;

/// <summary>
/// How the organisation's analyses are written (Adım 20.6). An organisation setting, so its
/// Admins' alone — reads included (Adım 16.5).
/// </summary>
[ApiController]
[Route("api/ai-settings")]
[Authorize(Policy = PlatformPolicies.Administer)]
public sealed class AiSettingsController : ControllerBase
{
    private readonly ISender _sender;

    public AiSettingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new GetAiSettingsQuery(), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveAiSettingsRequest request, CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new SaveAiSettingsCommand(request.ResponseLanguage), cancellationToken));

    public sealed record SaveAiSettingsRequest(AnalysisLanguage ResponseLanguage);
}
