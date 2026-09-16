using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public sealed record IntegrationDto
{
    // Settings whose name suggests a credential are never echoed back.
    private const string MaskedValue = "***";

    private static readonly string[] SensitiveKeyMarkers =
    [
        "password",
        "token",
        "secret",
        "apikey",
        "credential",
    ];

    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required NotificationChannelType Channel { get; init; }
    public required bool IsEnabled { get; init; }

    // Credential-looking values are masked: reading an integration back must not hand out the
    // customer's SMTP password or Jira token. Writes still take the real values.
    public required IReadOnlyDictionary<string, string> Config { get; init; }

    public IncidentPriority? MinPriority { get; init; }
    public string? CategoryFilter { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static IntegrationDto FromDomain(Integration integration)
    {
        return new IntegrationDto
        {
            Id = integration.Id,
            Name = integration.Name,
            Channel = integration.Channel,
            IsEnabled = integration.IsEnabled,
            Config = integration.Config.ToDictionary(
                pair => pair.Key,
                pair => IsSensitive(pair.Key) ? MaskedValue : pair.Value
            ),
            MinPriority = integration.MinPriority,
            CategoryFilter = integration.CategoryFilter,
            CreatedAt = integration.CreatedAt,
            UpdatedAt = integration.UpdatedAt,
        };
    }

    private static bool IsSensitive(string key)
    {
        return SensitiveKeyMarkers.Any(marker =>
            key.Contains(marker, StringComparison.OrdinalIgnoreCase)
        );
    }
}
