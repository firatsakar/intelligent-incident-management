using MediatR;
using Microsoft.AspNetCore.Mvc;
using TelemetryIngestionService.Application.Queries.GetEvidence;
using TelemetryIngestionService.Application.Queries.GetSignals;
using TelemetryIngestionService.Application.Queries.GetTelemetryStats;
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

    // Without a window: the queue for one status, defaulting to the weak band — recorded,
    // explained, but not worth waking anyone over.
    //
    // With a window: everything detected in that span across all statuses, which is what an
    // aggregate view needs. Filtering by status first would hide the contrast between the bursts
    // that were promoted and the ones that were not.
    [HttpGet("signals")]
    public async Task<IActionResult> GetSignals(
        [FromQuery] SignalStatus status = SignalStatus.Weak,
        [FromQuery] int limit = 50,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default
    )
    {
        // Half a window is a mistake worth naming rather than quietly ignoring.
        if (from.HasValue != to.HasValue)
            return BadRequest(new { error = "'from' and 'to' must be supplied together." });

        if (from >= to)
            return BadRequest(new { error = "'from' must be earlier than 'to'." });

        return Ok(
            await _sender.Send(
                new GetSignalsQuery(status, limit, from, to, offset),
                cancellationToken
            )
        );
    }

    // The detection gate's own story as numbers: how many log records folded into how many
    // signatures, how many of those crossed a rule, and how many of those the gate deliberately
    // did not raise. Plus the same rollup per service.
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default
    )
    {
        if (from.HasValue && to.HasValue && from >= to)
            return BadRequest(new { error = "'from' must be earlier than 'to'." });

        return Ok(await _sender.Send(new GetTelemetryStatsQuery(from, to), cancellationToken));
    }
}
