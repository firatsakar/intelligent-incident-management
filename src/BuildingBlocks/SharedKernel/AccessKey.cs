using System.Security.Cryptography;
using System.Text;

namespace BuildingBlocks.SharedKernel;

/// <summary>
/// A credential a machine sends with every request instead of signing in: the OTLP ingest key
/// (Adım 13.5) and the incident API key (Adım 27).
/// </summary>
/// <remarks>
/// 256 random bits behind a recognisable prefix — the prefix is what makes a leaked key findable by
/// a secret scanner and tells a person reading a config file what it is. SHA-256 rather than a
/// password hash, like the refresh token: there is nothing to guess, and the row has to be found
/// by the hash on every request.
/// </remarks>
public static class AccessKey
{
    // Enough of the key to tell two apart on a screen; far too little to use.
    private const int DisplayLength = 6;

    public readonly record struct Issued(string Key, string Hash, string DisplayPrefix);

    public static Issued Generate(string prefix)
    {
        var key = prefix + Base64Url(RandomNumberGenerator.GetBytes(32));

        return new Issued(key, Hash(key), key[..(prefix.Length + DisplayLength)]);
    }

    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
