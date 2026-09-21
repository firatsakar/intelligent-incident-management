namespace BuildingBlocks.Application;

// Customer credentials live in a free-form config bag, and both services that hold one mask it on
// read. That protects the API, but on its own it makes writes destructive: the only value a client
// can send back for a secret is the mask, and storing that silently replaces the real password
// with "***" — the integration then fails on the next real incident, with nothing to point at.
//
// Shared because the rule has to be identical on both sides of the round trip. It was written out
// twice already (IntegrationDto and TelemetrySourceDto) and diverging on what counts as a
// credential would mean masking a value on read and then failing to restore it on write.
public static class ConfigMasking
{
    /// <summary>What a masked value reads as. Never a legitimate setting.</summary>
    public const string MaskedValue = "***";

    private static readonly string[] SensitiveKeyMarkers =
    [
        "password",
        "token",
        "secret",
        "apikey",
        "credential",
    ];

    public static bool IsSensitive(string key)
    {
        return SensitiveKeyMarkers.Any(marker =>
            key.Contains(marker, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>Hides credential-looking values so a read never hands one back.</summary>
    public static IReadOnlyDictionary<string, string> Mask(IReadOnlyDictionary<string, string> config)
    {
        return config.ToDictionary(pair => pair.Key, pair => IsSensitive(pair.Key) ? MaskedValue : pair.Value);
    }

    /// <summary>
    /// The other half of the round trip: a submitted value that is still the mask means "keep what
    /// is stored", so an edit form can send back exactly what it was given without destroying a
    /// secret it was never allowed to see.
    ///
    /// A mask for a key that does not exist yet is left alone rather than invented — there is
    /// nothing to restore, and the per-channel validation should reject it as the missing value it
    /// is.
    /// </summary>
    public static Dictionary<string, string> Restore(
        IReadOnlyDictionary<string, string> submitted,
        IReadOnlyDictionary<string, string> stored
    )
    {
        var merged = new Dictionary<string, string>(submitted.Count);

        foreach (var (key, value) in submitted)
        {
            merged[key] =
                value == MaskedValue && stored.TryGetValue(key, out var existing) ? existing : value;
        }

        return merged;
    }
}
