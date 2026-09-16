using NotificationService.Domain.Enums;

namespace NotificationService.API.Contracts;

public sealed record CreateIntegrationRequest
{
    public required string Name { get; init; }
    public required NotificationChannelType Channel { get; init; }

    // Channel-specific settings. Required keys per channel: Email needs Host, Port, From and To;
    // Webhook needs Url; Jira needs BaseUrl, ProjectKey, Email and ApiToken.
    public required Dictionary<string, string> Config { get; init; }

    public IncidentPriority? MinPriority { get; init; }
    public string? CategoryFilter { get; init; }
    public bool IsEnabled { get; init; } = true;
}
