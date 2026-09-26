using System.Net;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Tests;

// Adım 26: an installation puts its own TLS proxy in front of the gateway. The scheme the browser
// used decides whether the session cookies are Secure; the caller's address decides which bucket
// the sign-in rate limiter counts against. These run the framework's own middleware with the
// options the gateway and the identity service install, so what is pinned is the behaviour, not
// the settings.
public sealed class PlatformForwardedHeadersTests
{
    private static async Task<HttpContext> Through(
        ForwardedHeadersOptions options,
        string from,
        params (string Name, string Value)[] headers
    )
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(from);

        foreach (var (name, value) in headers)
            context.Request.Headers[name] = value;

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options)
        );

        await middleware.Invoke(context);

        return context;
    }

    private static IConfiguration Proxies(string? value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { [PlatformForwardedHeaders.KnownProxiesConfigurationKey] = value }
            )
            .Build();

    public sealed class BehindTheGateway
    {
        [Fact]
        public async Task TheGatewaysHttpsMakesTheRequestHttpsFromAnyContainerAddress()
        {
            var context = await Through(
                PlatformForwardedHeaders.BehindGateway(),
                "172.18.0.7",
                ("X-Forwarded-Proto", "https")
            );

            Assert.True(context.Request.IsHttps);
        }

        [Fact]
        public async Task PlainHttpStaysPlainSoDevelopmentCookiesAreNotSecure()
        {
            var forwardedHttp = await Through(
                PlatformForwardedHeaders.BehindGateway(),
                "127.0.0.1",
                ("X-Forwarded-Proto", "http")
            );
            var nothingForwarded = await Through(PlatformForwardedHeaders.BehindGateway(), "127.0.0.1");

            Assert.False(forwardedHttp.Request.IsHttps);
            Assert.False(nothingForwarded.Request.IsHttps);
        }

        [Fact]
        public async Task TheCallersAddressIsNotTakenFromTheHeader()
        {
            var context = await Through(
                PlatformForwardedHeaders.BehindGateway(),
                "172.18.0.7",
                ("X-Forwarded-For", "203.0.113.9")
            );

            Assert.Equal(IPAddress.Parse("172.18.0.7"), context.Connection.RemoteIpAddress);
        }
    }

    public sealed class AtTheEdge
    {
        [Fact]
        public void WithNoProxyConfiguredNothingIsBelieved()
        {
            Assert.Null(PlatformForwardedHeaders.ForEdge(Proxies(null)));
            Assert.Null(PlatformForwardedHeaders.ForEdge(Proxies("  ")));
        }

        [Fact]
        public async Task AConfiguredProxyReportsTheCallersAddressAndScheme()
        {
            var options = PlatformForwardedHeaders.ForEdge(Proxies("10.0.0.5"))!;

            var context = await Through(
                options,
                "10.0.0.5",
                ("X-Forwarded-For", "198.51.100.23"),
                ("X-Forwarded-Proto", "https")
            );

            Assert.Equal(IPAddress.Parse("198.51.100.23"), context.Connection.RemoteIpAddress);
            Assert.True(context.Request.IsHttps);
        }

        [Fact]
        public async Task AnyoneElseCannotChooseTheRateLimitersBucket()
        {
            var options = PlatformForwardedHeaders.ForEdge(Proxies("10.0.0.5"))!;

            var context = await Through(
                options,
                "203.0.113.9",
                ("X-Forwarded-For", "198.51.100.23"),
                ("X-Forwarded-Proto", "https")
            );

            Assert.Equal(IPAddress.Parse("203.0.113.9"), context.Connection.RemoteIpAddress);
            Assert.False(context.Request.IsHttps);
        }

        [Fact]
        public async Task LoopbackIsNotTrustedUnlessNamed()
        {
            var options = PlatformForwardedHeaders.ForEdge(Proxies("10.0.0.5"))!;

            var context = await Through(options, "127.0.0.1", ("X-Forwarded-For", "198.51.100.23"));

            Assert.Equal(IPAddress.Loopback, context.Connection.RemoteIpAddress);
        }

        [Fact]
        public async Task ANetworkCoversEveryAddressInIt()
        {
            // A proxy on the Docker host reaches a published port from the bridge's address.
            var options = PlatformForwardedHeaders.ForEdge(Proxies("10.0.0.5, 172.16.0.0/12"))!;

            var context = await Through(options, "172.19.0.1", ("X-Forwarded-For", "198.51.100.23"));

            Assert.Equal(IPAddress.Parse("198.51.100.23"), context.Connection.RemoteIpAddress);
        }

        [Fact]
        public void AnEntryThatIsNeitherAnAddressNorANetworkStopsTheGateway()
        {
            var error = Assert.Throws<InvalidOperationException>(
                () => PlatformForwardedHeaders.ForEdge(Proxies("10.0.0.5, proxy.internal"))
            );

            Assert.Contains("proxy.internal", error.Message);
            Assert.Contains(PlatformForwardedHeaders.KnownProxiesConfigurationKey, error.Message);
        }
    }
}
