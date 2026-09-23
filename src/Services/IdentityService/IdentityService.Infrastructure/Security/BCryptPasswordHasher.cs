using IdentityService.Application.Abstractions;

namespace IdentityService.Infrastructure.Security;

/// <summary>
/// BCrypt, at a work factor chosen here rather than left at the library's default.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    // 2^12 rounds. The default is 11; the figure worth tracking is not the exponent but the time
    // it buys, which should stay in the region of a couple of hundred milliseconds on the hardware
    // that runs this. Fast enough that a sign-in does not feel stalled, slow enough that a leaked
    // table is not a wordlist away from being readable.
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        // A stored value this library cannot read — a row written by something else, or one that
        // survived a format change. That is a sign-in that fails, not a request that errors: the
        // caller asked whether this password matches, and the answer is no.
        //
        // Three exception types rather than one because the library reports a malformed hash three
        // different ways depending on how it is malformed: a bad salt prefix raises
        // SaltParseException, a truncated one reaches a Substring and raises
        // ArgumentOutOfRangeException, and a bad Base64 body raises FormatException. Catching only
        // the first — which is what this originally did — turned two of the three into a 500.
        catch (Exception ex)
            when (ex is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            return false;
        }
    }
}
