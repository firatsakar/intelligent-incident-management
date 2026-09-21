using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.API.Contracts;

public sealed record CreateTelemetrySourceRequest
{
    public required string Name { get; init; }
    public required TelemetrySourceKind Kind { get; init; }

    // Connector-specific settings. A Seq source needs Url and ApiKey; Filter and ServiceProperty
    // are optional overrides.
    public required Dictionary<string, string> Config { get; init; }

    public int? PollIntervalSeconds { get; init; }
    public bool IsEnabled { get; init; } = true;
}
