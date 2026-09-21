using MediatR;
using Microsoft.AspNetCore.Mvc;
using TelemetryIngestionService.Application.Queries.GetEvidence;
using TelemetryIngestionService.Application.Queries.GetSignals;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.API.Controllers;

[ApiController]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(30);

    private readonly ISender _sender;

    public TelemetryController(ISender sender)
    {
        _sender = sender;
    }

    // What happened in a window: logs, the signatures they rolled up into, and the signals those
    // produced. For humans and the Adım 19 frontend — the AI reads the evidence summary carried
    // on the incident instead.
    [HttpGet("evidence")]
    public async Task<IActionResult> GetEvidence(
        [FromQuery] string? service,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken
    )
    {
        var end = to ?? DateTime.UtcNow;
        var start = from ?? end - DefaultWindow;

        if (start >= end)
            return BadRequest(new { error = "'from' must be earlier than 'to'." });

        return Ok(await _sender.Send(new GetEvidenceQuery(service, start, end), cancellationToken));
    }

    // The weak-signal queue: recorded, explained, but not worth waking anyone over.
    [HttpGet("signals")]
    public async Task<IActionResult> GetSignals(
        [FromQuery] SignalStatus status = SignalStatus.Weak,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default
    )
    {
        return Ok(await _sender.Send(new GetSignalsQuery(status, limit), cancellationToken));
    }
}
