namespace TelemetryIngestionService.API.Contracts;

public sealed record SetTelemetrySourceEnabledRequest
{
    public required bool IsEnabled { get; init; }
}
