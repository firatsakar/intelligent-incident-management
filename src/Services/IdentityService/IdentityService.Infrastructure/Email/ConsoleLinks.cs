using IdentityService.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Email;

// The token goes in the path, not the query string: a query string is what proxies and analytics
// log by default. It is URL-safe base64 already, so nothing needs escaping.
public sealed class ConsoleLinks : IConsoleLinks
{
    private readonly string _base;

    public ConsoleLinks(IOptions<ConsoleOptions> options)
    {
        _base = options.Value.BaseUrl.TrimEnd('/');
    }

    public string Invitation(string token) => $"{_base}/invite/{token}";

    public string PasswordReset(string token) => $"{_base}/reset/{token}";
}
