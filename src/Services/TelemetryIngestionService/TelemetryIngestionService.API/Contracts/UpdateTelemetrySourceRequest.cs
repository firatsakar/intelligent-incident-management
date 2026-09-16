namespace TelemetryIngestionService.API.Contracts;

// The kind is fixed at creation, so it cannot be changed here. Config is replaced wholesale.
public sealed record UpdateTelemetrySourceRequest
{
    public required string Name { get; init; }
    public required Dictionary<string, string> Config { get; init; }
    public int? PollIntervalSeconds { get; init; }
}
