using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Validators;

// The config bag is schemaless by design, so required keys per kind are enforced here — a
// misconfigured source should be rejected at the API, not discovered when the poller goes quiet.
public static class TelemetrySourceConfigRules
{
    /// <summary>
    /// The least severe record a pushed source keeps. Warning or Error; Error when unset, which
    /// is what the Seq connector selects by default too. Anything below is dropped at the door —
    /// the collector should have filtered it, and this is the answer for when it did not.
    /// </summary>
    public const string MinimumSeverityKey = "MinimumSeverity";

    private static readonly LogSeverity[] AllowedMinimumSeverities = [LogSeverity.Warning, LogSeverity.Error];

    private static readonly Dictionary<TelemetrySourceKind, string[]> RequiredKeys = new()
    {
        // Only the URL is required. A Seq instance with authentication enabled answers 401 without
        // an ApiKey, but an unsecured one reads fine without it — so the key is optional and a
        // missing one surfaces as a clear failure from the test endpoint rather than as a
        // validation error at configuration time.
        [TelemetrySourceKind.Seq] = ["Url"],

        // Nothing: a pushed source is configured on the sender's side. The key is issued, not
        // entered.
        [TelemetrySourceKind.Otlp] = [],
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

    /// <summary>A reason the config cannot be accepted beyond a missing key, or null when it can.</summary>
    public static string? Invalid(
        TelemetrySourceKind kind,
        IReadOnlyDictionary<string, string>? config
    )
    {
        if (kind != TelemetrySourceKind.Otlp || config is null)
            return null;

        if (!config.TryGetValue(MinimumSeverityKey, out var value) || string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<LogSeverity>(value, ignoreCase: true, out var severity)
            && AllowedMinimumSeverities.Contains(severity)
            ? null
            : $"{MinimumSeverityKey} must be Warning or Error.";
    }

    /// <summary>What a pushed source keeps, read the same way the validator accepted it.</summary>
    public static LogSeverity MinimumSeverity(IReadOnlyDictionary<string, string> config) =>
        config.TryGetValue(MinimumSeverityKey, out var value)
        && Enum.TryParse<LogSeverity>(value, ignoreCase: true, out var severity)
        && AllowedMinimumSeverities.Contains(severity)
            ? severity
            : LogSeverity.Error;

    public static string Describe(TelemetrySourceKind kind, IReadOnlyList<string> missing)
    {
        return $"A {kind} telemetry source requires the setting(s): {string.Join(", ", missing)}.";
    }
}
