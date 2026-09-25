using System.Security.Cryptography;
using System.Text;

namespace TelemetryIngestionService.Domain.Services;

/// <summary>
/// The credential a pushed source sends with every batch.
/// </summary>
/// <remarks>
/// 256 random bits behind a recognisable prefix — the prefix is what makes a leaked key findable by
/// a secret scanner and tells a person reading a collector config what it is. SHA-256 rather than
/// a password hash, like the refresh token: there is nothing to guess, and the row has to be found
/// by the hash on every request.
/// </remarks>
public static class IngestKey
{
    public const string Prefix = "iim_otlp_";

    // Enough of the key to tell two apart on a screen; far too little to use.
    private const int DisplayLength = 6;

    public readonly record struct Issued(string Key, string Hash, string DisplayPrefix);

    public static Issued Generate()
    {
        var key = Prefix + Base64Url(RandomNumberGenerator.GetBytes(32));

        return new Issued(key, Hash(key), key[..(Prefix.Length + DisplayLength)]);
    }

    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
