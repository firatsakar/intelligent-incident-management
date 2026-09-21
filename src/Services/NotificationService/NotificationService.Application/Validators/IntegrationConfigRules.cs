using NotificationService.Domain.Enums;

namespace NotificationService.Application.Validators;

// The config bag is schemaless by design, so the required keys per channel are enforced here
// instead — a misconfigured integration should be rejected at the API, not discovered when an
// incident fires.
public static class IntegrationConfigRules
{
    private static readonly Dictionary<NotificationChannelType, string[]> RequiredKeys = new()
    {
        [NotificationChannelType.Email] = ["Host", "Port", "From", "To"],
        [NotificationChannelType.Webhook] = ["Url"],
        [NotificationChannelType.Jira] = ["BaseUrl", "ProjectKey", "Email", "ApiToken"],
    };

    public static IReadOnlyList<string> MissingKeys(
        NotificationChannelType channel,
        IReadOnlyDictionary<string, string>? config
    )
    {
        if (!RequiredKeys.TryGetValue(channel, out var required))
            return [];

        config ??= new Dictionary<string, string>();

        return required
            .Where(key =>
                !config.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)
            )
            .ToList();
    }

    public static string Describe(NotificationChannelType channel, IReadOnlyList<string> missing)
    {
        return $"A {channel} integration requires the setting(s): {string.Join(", ", missing)}.";
    }
}
