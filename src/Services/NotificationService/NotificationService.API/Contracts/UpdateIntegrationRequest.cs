using NotificationService.Domain.Enums;

namespace NotificationService.API.Contracts;

// The channel is fixed at creation, so it cannot be changed here. Config is replaced wholesale.
public sealed record UpdateIntegrationRequest
{
    public required string Name { get; init; }
    public required Dictionary<string, string> Config { get; init; }
    public IncidentPriority? MinPriority { get; init; }
    public string? CategoryFilter { get; init; }
}
