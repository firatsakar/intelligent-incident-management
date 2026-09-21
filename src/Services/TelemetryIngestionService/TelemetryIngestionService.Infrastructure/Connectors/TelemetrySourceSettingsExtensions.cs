using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Connectors;

// Config is a free-form jsonb bag, so each connector states which keys it needs and fails loudly
// with the source's name when one is missing.
internal static class TelemetrySourceSettingsExtensions
{
    public static string RequiredSetting(this TelemetrySource source, string key)
    {
        return source.OptionalSetting(key)
            ?? throw new InvalidOperationException(
                $"Telemetry source '{source.Name}' is missing the required setting '{key}'."
            );
    }

    public static string? OptionalSetting(this TelemetrySource source, string key)
    {
        return source.Config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    public static string SettingOrDefault(this TelemetrySource source, string key, string fallback)
    {
        return source.OptionalSetting(key) ?? fallback;
    }
}
