using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Web;

/// <summary>
/// What the gateway and the services behind it believe about how a request reached them
/// (Adım 26).
/// </summary>
/// <remarks>
/// <para>
/// An installation puts TLS in front of the gateway — the installer's own proxy — so the gateway
/// sees plain HTTP from that proxy, and the identity service sees plain HTTP from the gateway. The
/// scheme the browser actually used arrives in <c>X-Forwarded-Proto</c>, and it decides whether
/// the session cookies are <c>Secure</c>.
/// </para>
/// <para>
/// Believing the headers is not free. At the edge, <c>X-Forwarded-For</c> replaces the caller's
/// address, and the sign-in rate limiter partitions by that address: believed from anyone, a
/// guesser would name a new address with every attempt. So the gateway believes only the proxies
/// it is told about, and with none configured believes nothing, as before.
/// </para>
/// </remarks>
public static class PlatformForwardedHeaders
{
    /// <summary>
    /// The proxies in front of the gateway, comma-separated: addresses (<c>10.0.0.5</c>) or
    /// networks (<c>172.16.0.0/12</c>).
    /// </summary>
    public const string KnownProxiesConfigurationKey = "ForwardedHeaders:KnownProxies";

    /// <summary>
    /// For the gateway: the caller's address and scheme as the configured proxies report them, or
    /// <c>null</c> — do not install the middleware — when no proxy is configured.
    /// </summary>
    public static ForwardedHeadersOptions? ForEdge(IConfiguration configuration)
    {
        var configured = configuration[KnownProxiesConfigurationKey];

        if (string.IsNullOrWhiteSpace(configured))
            return null;

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        };

        // The defaults trust loopback. Only what is configured is trusted here.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var entry in configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Contains('/') && System.Net.IPNetwork.TryParse(entry, out var network))
                options.KnownIPNetworks.Add(network);
            else if (!entry.Contains('/') && IPAddress.TryParse(entry, out var address))
                options.KnownProxies.Add(address);
            else
                throw new InvalidOperationException(
                    $"{KnownProxiesConfigurationKey} has an entry that is neither an address nor a network: '{entry}'."
                );
        }

        return options;
    }

    /// <summary>
    /// For a service behind the gateway: the scheme the gateway reports, and nothing else. Its
    /// port is published to nobody in an installation, so the gateway is the only one who can
    /// send the header; the caller's address is the gateway's business, not this service's.
    /// </summary>
    public static ForwardedHeadersOptions BehindGateway()
    {
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };

        // Empty lists mean any sender: in a container network the gateway's address is not known
        // in advance.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        return options;
    }
}
