using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Domain.Services;

/// <summary>
/// The secret in an invitation or password-reset link.
/// </summary>
/// <remarks>
/// The same shape as a refresh token, for the same reasons (see <c>RefreshToken</c>): 256 random
/// bits, so there is nothing to guess and a slow hash would defend against nothing; and SHA-256
/// rather than BCrypt, because the row has to be found by what the link presents. Only the hash is
/// stored. The token itself exists in the response that issued it and in the email — nowhere else.
/// </remarks>
public static class OneTimeToken
{
    public readonly record struct Issued(string Token, string Hash);

    public static Issued Generate()
    {
        var token = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new Issued(token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
