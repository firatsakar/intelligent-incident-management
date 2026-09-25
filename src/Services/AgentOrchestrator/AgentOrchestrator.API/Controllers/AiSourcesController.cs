using AgentOrchestrator.Application.Commands.DeleteGitHubConnection;
using AgentOrchestrator.Application.Commands.SaveGitHubConnection;
using AgentOrchestrator.Application.Commands.TestGitHubConnection;
using AgentOrchestrator.Application.DTOs;
using AgentOrchestrator.Application.Queries.GetGitHubConnection;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.API.Controllers;

/// <summary>
/// The outside systems an analysis may read, as the organisation configured them (Adım 17.5).
/// The organisation's own, and so its Admins' alone — reads included, as with integrations.
/// </summary>
[ApiController]
[Route("api/ai-sources")]
[Authorize(Policy = PlatformPolicies.Administer)]
public sealed class AiSourcesController : ControllerBase
{
    private readonly ISender _sender;

    public AiSourcesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("github")]
    public async Task<IActionResult> GetGitHub(CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new GetGitHubConnectionQuery(), cancellationToken));

    [HttpPut("github")]
    public async Task<IActionResult> SaveGitHub(
        [FromBody] SaveGitHubRequest request,
        CancellationToken cancellationToken
    ) =>
        Ok(
            await _sender.Send(
                new SaveGitHubConnectionCommand(request.Token, request.IsEnabled, request.Repositories ?? []),
                cancellationToken
            )
        );

    /// <summary>
    /// Reads each mapped repository once with the stored token and says, per repository, whether
    /// it worked — GitHub's own reason when it did not. Reads only.
    /// </summary>
    [HttpPost("github/test")]
    public async Task<IActionResult> TestGitHub(CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new TestGitHubConnectionCommand(), cancellationToken));

    [HttpDelete("github")]
    public async Task<IActionResult> DeleteGitHub(CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteGitHubConnectionCommand(), cancellationToken);

        return NoContent();
    }

    public sealed record SaveGitHubRequest(
        string? Token,
        bool IsEnabled,
        IReadOnlyList<RepositoryMappingDto>? Repositories
    );
}
