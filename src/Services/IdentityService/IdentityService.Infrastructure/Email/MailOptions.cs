namespace IdentityService.Infrastructure.Email;

/// <summary>
/// The platform's own mail server. Defaults point at the development Mailpit (SMTP on 1025, no
/// authentication, no TLS), so a clean checkout sends somewhere that can be looked at; production
/// sets all of this from its environment.
/// </summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public string From { get; set; } = "no-reply@iim.local";

    public string FromName { get; set; } = "IIM";

    /// <summary>Empty for Mailpit. Set both for a server that authenticates.</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>STARTTLS when the server offers it. Off only for a local test server.</summary>
    public bool UseTls { get; set; }
}

/// <summary>Where the console answers, for links in email.</summary>
public sealed class ConsoleOptions
{
    public const string SectionName = "Console";

    public string BaseUrl { get; set; } = "http://localhost:5173";
}
