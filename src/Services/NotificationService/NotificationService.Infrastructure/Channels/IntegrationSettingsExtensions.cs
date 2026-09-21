using NotificationService.Domain.Aggregates;

namespace NotificationService.Infrastructure.Channels;

// Config is a free-form jsonb bag, so each channel states which keys it needs and fails loudly
// with the integration's name when one is missing or unusable.
internal static class IntegrationSettingsExtensions
{
    public static string RequiredSetting(this Integration integration, string key)
    {
        var value = integration.OptionalSetting(key);

        return value
            ?? throw new InvalidOperationException(
                $"Integration '{integration.Name}' is missing the required setting '{key}'."
            );
    }

    public static string? OptionalSetting(this Integration integration, string key)
    {
        return integration.Config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    public static int RequiredIntSetting(this Integration integration, string key)
    {
        var value = integration.RequiredSetting(key);

        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Integration '{integration.Name}' has a non-numeric value for the setting '{key}': '{value}'."
            );
    }
}
