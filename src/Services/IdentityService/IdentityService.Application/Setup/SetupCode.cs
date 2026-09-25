using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Application.Setup;

/// <summary>
/// The one-time code that guards the first-run setup (Adım 25).
/// </summary>
/// <remarks>
/// <para>
/// An empty installation has to let somebody create its first Admin, and whoever reaches the
/// setup screen first would otherwise be that somebody — on a server exposed to the internet, that
/// is not necessarily the person who installed it. The code is written to the identity service's
/// log when it starts on an empty database, and the setup screen asks for it: only someone who can
/// read the server's logs can claim the installation. Jenkins does the same with its initial
/// admin password.
/// </para>
/// <para>
/// Held in memory, never stored: it exists only while the database is empty and the process is
/// up. A restart issues a new one and logs it again; completing the setup forgets it for good.
/// Twelve characters from an alphabet without look-alikes (no 0/O, 1/I/L, U/V) — about 58
/// bits (29 symbols, twelve of them), typed from a log line, and the endpoint is rate-limited
/// besides.
/// </para>
/// </remarks>
public sealed class SetupCode
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTWXYZ23456789";
    private const int Length = 12;

    private readonly Lock _sync = new();
    private string? _code;

    /// <summary>One setup at a time: the check for "still no users" and the writes happen under it.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public bool IsIssued
    {
        get
        {
            lock (_sync)
                return _code is not null;
        }
    }

    /// <summary>A fresh code, replacing any earlier one. Returned formatted as XXXX-XXXX-XXXX.</summary>
    public string Issue()
    {
        var code = new string(RandomNumberGenerator.GetItems<char>(Alphabet, Length));

        lock (_sync)
            _code = code;

        return $"{code[..4]}-{code[4..8]}-{code[8..]}";
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> is the current code. Dashes, spaces and case are
    /// forgiven — it is typed by a person from a log line — and the comparison takes the same time
    /// however much of it is right.
    /// </summary>
    public bool Matches(string? candidate)
    {
        string? code;

        lock (_sync)
            code = _code;

        if (code is null || string.IsNullOrWhiteSpace(candidate))
            return false;

        var normalized = new string(candidate.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(normalized), Encoding.UTF8.GetBytes(code));
    }

    /// <summary>The setup happened; nothing opens it again.</summary>
    public void Consume()
    {
        lock (_sync)
            _code = null;
    }
}
