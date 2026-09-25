namespace IncidentService.Domain.ValueObjects;

/// <summary>
/// A commit the analysis named as a likely cause (Adım 17.5): what the console links to. Written
/// by the platform from what GitHub returned, never by the model.
/// </summary>
public sealed record AiRelatedChange(string Sha, string Title, string? Author, DateTime CommittedAt, string Url)
{
    /// <summary>
    /// The only links this service keeps: https to github.com. The agent builds them that way; this
    /// is the second check, at the edge of the service that hands them to a browser.
    /// </summary>
    public static bool IsSafeUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host == "github.com";
}
