using BuildingBlocks.Application;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public sealed record IntegrationDto
{
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
            // The masking rule is shared with the update handler on purpose: the same definition
            // of "looks like a credential" has to decide what is hidden on read and what is
            // restored on write, or a secret gets masked out and never put back.
            Config = ConfigMasking.Mask(integration.Config),
            MinPriority = integration.MinPriority,
            CategoryFilter = integration.CategoryFilter,
            CreatedAt = integration.CreatedAt,
            UpdatedAt = integration.UpdatedAt,
        };
    }
}
