using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.API.Controllers;

// A local target for exercising webhook integrations without depending on an external service.
// Development only — it echoes whatever it is given straight into the logs.
[ApiController]
[Route("api/dev")]
public sealed class DevController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DevController> _logger;

    public DevController(IWebHostEnvironment environment, ILogger<DevController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    // The catch-all suffix lets the echo stand in for an API that appends its own path, so a
    // channel's request can be inspected before it is ever pointed at the real service.
    [HttpPost("webhook-echo")]
    [HttpPost("webhook-echo/{**path}")]
    public async Task<IActionResult> WebhookEcho(string? path, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        // Header names only — values can carry credentials.
        _logger.LogInformation(
            "Webhook echo received POST /{Path} with headers [{HeaderNames}] and payload: {Payload}",
            path ?? string.Empty,
            string.Join(", ", Request.Headers.Select(header => header.Key)),
            body
        );

        return Ok(new { receivedAt = DateTime.UtcNow, payload = JsonDocument.Parse(body).RootElement });
    }
}
