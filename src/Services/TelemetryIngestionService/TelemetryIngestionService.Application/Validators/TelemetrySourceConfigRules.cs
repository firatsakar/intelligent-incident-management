using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Validators;

// The config bag is schemaless by design, so required keys per kind are enforced here — a
// misconfigured source should be rejected at the API, not discovered when the poller goes quiet.
public static class TelemetrySourceConfigRules
{
    private static readonly Dictionary<TelemetrySourceKind, string[]> RequiredKeys = new()
    {
        // Only the URL is required. A Seq instance with authentication enabled answers 401 without
        // an ApiKey, but an unsecured one reads fine without it — so the key is optional and a
        // missing one surfaces as a clear failure from the test endpoint rather than as a
        // validation error at configuration time.
        [TelemetrySourceKind.Seq] = ["Url"],
    };

    public static IReadOnlyList<string> MissingKeys(
        TelemetrySourceKind kind,
        IReadOnlyDictionary<string, string>? config
    )
    {
        if (!RequiredKeys.TryGetValue(kind, out var required))
            return [];

        config ??= new Dictionary<string, string>();

        return required
            .Where(key =>
                !config.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)
            )
            .ToList();
    }

    public static string Describe(TelemetrySourceKind kind, IReadOnlyList<string> missing)
    {
        return $"A {kind} telemetry source requires the setting(s): {string.Join(", ", missing)}.";
    }
}
