namespace IdentityService.Application.Abstractions;

/// <summary>
/// Where the console answers, for the links that go out in email. In development the console's own
/// dev server; in production the gateway's public address, which serves the console too.
/// </summary>
public interface IConsoleLinks
{
    string Invitation(string token);

    string PasswordReset(string token);
}
