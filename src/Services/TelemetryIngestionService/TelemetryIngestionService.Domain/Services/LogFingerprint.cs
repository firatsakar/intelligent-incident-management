using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TelemetryIngestionService.Domain.Services;

// Turns a log event into the identity of the error behind it. Two failures are "the same error"
// when they come from the same service, throw the same exception type, and say the same thing
// once the volatile parts are removed.
public static partial class LogFingerprint
{
    private const int MaxNormalizedLength = 4096;

    // Order matters: a GUID is also a run of hex, and hex is also a run of digits, so the most
    // specific pattern has to win first.
    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.None,
        matchTimeoutMilliseconds: 200
    )]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"\b[0-9a-fA-F]{16,}\b", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex LongHexPattern();

    [GeneratedRegex(
        @"\b[\w.+-]+@[\w-]+\.[\w.-]+\b",
        RegexOptions.None,
        matchTimeoutMilliseconds: 200
    )]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex NumberPattern();

    // A message template is already normalised by construction — its volatile parts are named
    // placeholders the author chose. Using it beats any amount of guessing, and it is why
    // structured logging is worth insisting on upstream.
    public static string Normalize(string message, string? messageTemplate)
    {
        if (!string.IsNullOrWhiteSpace(messageTemplate))
            return Truncate(messageTemplate);

        var normalized = GuidPattern().Replace(message, "<id>");
        normalized = EmailPattern().Replace(normalized, "<email>");
        normalized = LongHexPattern().Replace(normalized, "<hex>");
        normalized = NumberPattern().Replace(normalized, "#");

        return Truncate(normalized);
    }

    public static string Compute(string service, string? exceptionType, string normalizedMessage)
    {
        // The separator keeps fields from bleeding into one another, so a service named "a|b"
        // cannot collide with a different service whose exception type starts with "b".
        var material = $"{service}{exceptionType ?? string.Empty}{normalizedMessage}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        // Half of SHA-256 is far more than enough to keep distinct errors apart, and it fits the
        // column comfortably.
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaxNormalizedLength ? value : value[..MaxNormalizedLength];
    }
}
