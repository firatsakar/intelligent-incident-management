using System.Security.Cryptography;
using System.Text;

namespace BuildingBlocks.SharedKernel;

/// <summary>
/// Encrypts the customer credentials a service has to keep in its database — an SMTP password, a
/// Jira token, a log store's API key, a GitHub token — so that the database, its backups and
/// anybody reading them do not hold them in the clear.
/// </summary>
/// <remarks>
/// <para>
/// AES-256-GCM with a random nonce per value: the same password saved twice is two different
/// ciphertexts, and a value altered in the database fails to decrypt rather than decrypting to
/// something else. The key comes from configuration (<c>Secrets:EncryptionKey</c>, 32 bytes as
/// base64) and never from the database it protects.
/// </para>
/// <para>
/// A stored value carries a prefix saying it is encrypted and by which version of this format. A
/// value without it is a row written before encryption existed, and is read as it is — the service
/// encrypts those on start, so the prefix-less path exists for exactly one start.
/// </para>
/// </remarks>
public sealed class SecretProtector
{
    public const string ConfigurationKey = "Secrets:EncryptionKey";

    public const string Prefix = "iim:enc:v1:";

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public SecretProtector(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("The encryption key must be 32 bytes (AES-256).", nameof(key));

        _key = key;
    }

    /// <summary>
    /// The key from configuration. There is no default and there must not be one: an encryption key
    /// with a fallback is an encryption key somebody else also knows.
    /// </summary>
    public static SecretProtector FromConfiguration(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException(
                $"{ConfigurationKey} is not configured. Set it to 32 random bytes as base64 — in user secrets in "
                    + "development, in the environment (Secrets__EncryptionKey) in production."
            );

        byte[] key;

        try
        {
            key = Convert.FromBase64String(base64Key.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{ConfigurationKey} is not valid base64.");
        }

        if (key.Length != 32)
            throw new InvalidOperationException($"{ConfigurationKey} must decode to 32 bytes; it decodes to {key.Length}.");

        return new SecretProtector(key);
    }

    public static bool IsProtected(string? value) => value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>The value encrypted. Empty stays empty, and an already encrypted value is left as it is.</summary>
    public string Protect(string plaintext)
    {
        if (plaintext.Length == 0 || IsProtected(plaintext))
            return plaintext;

        var input = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[NonceSize + TagSize + input.Length];

        var nonce = output.AsSpan(0, NonceSize);
        var tag = output.AsSpan(NonceSize, TagSize);
        var cipher = output.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, input, cipher, tag);

        return Prefix + Convert.ToBase64String(output);
    }

    /// <summary>
    /// The plain value. One written before encryption existed comes back as it is; one that was
    /// altered, or encrypted with another key, throws <see cref="CryptographicException"/>.
    /// </summary>
    public string Unprotect(string stored)
    {
        if (!IsProtected(stored))
            return stored;

        byte[] data;

        try
        {
            data = Convert.FromBase64String(stored[Prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("A stored secret is not valid.", ex);
        }

        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("A stored secret is truncated.");

        var output = new byte[data.Length - NonceSize - TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            data.AsSpan(0, NonceSize),
            data.AsSpan(NonceSize + TagSize),
            data.AsSpan(NonceSize, TagSize),
            output
        );

        return Encoding.UTF8.GetString(output);
    }
}
