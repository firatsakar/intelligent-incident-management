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

    [HttpPost("webhook-echo")]
    public async Task<IActionResult> WebhookEcho(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        _logger.LogInformation("Webhook echo received payload: {Payload}", body);

        return Ok(new { receivedAt = DateTime.UtcNow, payload = JsonDocument.Parse(body).RootElement });
    }
}
