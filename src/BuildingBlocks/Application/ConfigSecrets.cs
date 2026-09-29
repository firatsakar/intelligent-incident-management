using BuildingBlocks.SharedKernel;

namespace BuildingBlocks.Application;

/// <summary>
/// The credentials in a config bag, encrypted for the database and decrypted on the way back.
/// </summary>
/// <remarks>
/// Which values count as credentials is <see cref="ConfigMasking"/>'s rule, the one that masks them
/// on read, so what the API never hands back and what the database never holds in the clear are the
/// same set of keys by construction.
/// </remarks>
public static class ConfigSecrets
{
    public static Dictionary<string, string> Protect(IReadOnlyDictionary<string, string> config, SecretProtector secrets) =>
        config.ToDictionary(
            pair => pair.Key,
            pair => ConfigMasking.IsSensitive(pair.Key) ? secrets.Protect(pair.Value) : pair.Value
        );

    public static Dictionary<string, string> Unprotect(IReadOnlyDictionary<string, string> config, SecretProtector secrets) =>
        config.ToDictionary(
            pair => pair.Key,
            pair => ConfigMasking.IsSensitive(pair.Key) ? secrets.Unprotect(pair.Value) : pair.Value
        );

    /// <summary>True while a credential in it is still stored in the clear — a row saved before encryption at rest.</summary>
    public static bool HasPlaintextSecret(IReadOnlyDictionary<string, string> config) =>
        config.Any(pair =>
            ConfigMasking.IsSensitive(pair.Key) && pair.Value.Length > 0 && !SecretProtector.IsProtected(pair.Value)
        );
}
