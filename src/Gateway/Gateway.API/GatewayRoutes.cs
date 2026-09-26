using Yarp.ReverseProxy.Configuration;

namespace Gateway.API;

/// <summary>
/// The path to service map, and the only place it exists.
/// </summary>
/// <remarks>
/// It lived in <c>web/vite.config.ts</c> until now, which meant the browser could only reach the
/// services while a dev server was running: Vite defines <c>server.proxy</c> and nothing else, so
/// a built <c>dist/</c> had no way to call anything. This file is the same entries, moved to where
/// they work in development and in production alike.
///
/// In memory rather than in configuration on purpose. A route table is a structure with rules —
/// which prefixes belong to the same service, which ones carry a WebSocket, which one is worth
/// rate limiting — and those rules are worth stating in a language that can hold a comment. The
/// destination addresses are the part that genuinely varies per environment, so those alone are
/// read from configuration, the way <see cref="BuildingBlocks.Observability.LoggingExtensions"/>
/// reads the Seq URL.
/// </remarks>
internal static class GatewayRoutes
{
    /// <summary>Configuration section holding per-environment service addresses.</summary>
    public const string ServicesConfigurationSection = "Services";

    /// <summary>
    /// Applied to sign-in alone. It is the one endpoint where guessing is the attack, and where
    /// each guess also costs the server a BCrypt verification at work factor 12. Refresh is
    /// deliberately not limited: every open console calls it on a timer, and a limit there would
    /// sign people out for being logged in.
    /// </summary>
    public const string SignInRateLimiterPolicy = "sign-in";

    /// <summary>
    /// Applied to the incident API (Adım 27), per key: a sender misconfigured to post in a loop
    /// would otherwise open incidents as fast as it can send them. Far above what alerting sends.
    /// </summary>
    public const string IncidentIntakeRateLimiterPolicy = "incident-intake";

    /// <summary>The header the incident API reads its key from — IncidentIntakeController.ApiKeyHeader.</summary>
    public const string IncidentApiKeyHeader = "X-IIM-Api-Key";

    // One cluster per service, not per prefix: two of the five answer on more than one prefix and
    // a cluster is a destination, not a route.
    private const string IdentityCluster = "identity";
    private const string IncidentCluster = "incident";
    private const string AgentCluster = "agent";
    private const string NotificationCluster = "notification";
    private const string TelemetryCluster = "telemetry";

    private static readonly (string Cluster, string ConfigurationKey, string Fallback)[] Services =
    [
        (IdentityCluster, "Identity", "http://localhost:5240"),
        (IncidentCluster, "Incident", "http://localhost:5203"),
        (AgentCluster, "Agent", "http://localhost:5130"),
        (NotificationCluster, "Notification", "http://localhost:5210"),
        (TelemetryCluster, "Telemetry", "http://localhost:5220"),
    ];

    // The catch-all matches the prefix itself as well as everything under it, so `/api/incidents`
    // and `/api/incidents/{id}/status` both land on the same cluster.
    //
    // `/api/telemetry` and `/api/telemetry-sources` need no ordering here, unlike the Vite table
    // they replace: a route template matches whole segments, so `/api/telemetry/{**rest}` does not
    // swallow `/api/telemetry-sources`. Two entries, no precedence rule to remember. The same is
    // what lets the literal `/api/auth/login` sit beside `/api/auth/{**rest}` and win.
    private static readonly (string Route, string Path, string Cluster, string? RateLimiter)[] Api =
    [
        ("auth-sign-in", "/api/auth/login", IdentityCluster, SignInRateLimiterPolicy),
        // Every endpoint where a password is set or tested earns the same limit as signing in.
        // The one-time links are 256 bits and not worth guessing; the limit is for the password
        // behind the change-password form, and for a client hammering a link it holds.
        ("auth-invitations", "/api/auth/invitations/{**rest}", IdentityCluster, SignInRateLimiterPolicy),
        ("auth-password-resets", "/api/auth/password-resets/{**rest}", IdentityCluster, SignInRateLimiterPolicy),
        ("auth-password", "/api/auth/password", IdentityCluster, SignInRateLimiterPolicy),
        // Only completing the setup is limited: its status is asked by every visit to the sign-in
        // screen, and sharing the sign-in bucket would spend a person's attempts on page loads.
        ("auth-setup", "/api/auth/setup/complete", IdentityCluster, SignInRateLimiterPolicy),
        ("auth", "/api/auth/{**rest}", IdentityCluster, null),
        // The organisation's members — IdentityService, which is where accounts live.
        ("organization", "/api/organization/{**rest}", IdentityCluster, null),
        // External systems opening incidents with an API key (Adım 27); the literal wins over the
        // catch-all below, as sign-in does over /api/auth.
        ("incident-intake", "/api/incidents/intake", IncidentCluster, IncidentIntakeRateLimiterPolicy),
        ("incidents", "/api/incidents/{**rest}", IncidentCluster, null),
        ("incident-api-keys", "/api/incident-api-keys/{**rest}", IncidentCluster, null),
        // Routed because the endpoint exists and is reachable; the console never calls it. The
        // normal trigger for an analysis is IncidentDetectedEvent over RabbitMQ.
        ("analyses", "/api/analyses/{**rest}", AgentCluster, null),
        ("ai-sources", "/api/ai-sources/{**rest}", AgentCluster, null),
        ("notifications", "/api/notifications/{**rest}", NotificationCluster, null),
        ("integrations", "/api/integrations/{**rest}", NotificationCluster, null),
        ("telemetry", "/api/telemetry/{**rest}", TelemetryCluster, null),
        ("telemetry-sources", "/api/telemetry-sources/{**rest}", TelemetryCluster, null),
        // Not under /api: this is where customers' collectors send logs, and an OTLP exporter
        // given a base address appends /v1/logs to it. Authenticated by the source's ingest key
        // at the service, not by the session cookie, which a collector does not have.
        ("otlp", "/otlp/{**rest}", TelemetryCluster, null),
    ];

    // The hubs need nothing special from YARP — it proxies the upgrade itself, and the three
    // sockets stay three sockets on three services. What changes is that the browser now opens all
    // of them against one origin, which is what lets the session be a cookie rather than a token in
    // a query string.
    private static readonly (string Route, string Path, string Cluster)[] Hubs =
    [
        ("hub-incidents", "/hubs/incidents/{**rest}", IncidentCluster),
        ("hub-notifications", "/hubs/notifications/{**rest}", NotificationCluster),
        ("hub-signals", "/hubs/signals/{**rest}", TelemetryCluster),
    ];

    public static (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters) Build(
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection(ServicesConfigurationSection);

        var clusters = Services
            .Select(service => new ClusterConfig
            {
                ClusterId = service.Cluster,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    [service.Cluster] = new DestinationConfig
                    {
                        Address = section[service.ConfigurationKey] ?? service.Fallback,
                    },
                },
            })
            .ToArray();

        var routes = Api.Select(route => new RouteConfig
            {
                RouteId = route.Route,
                ClusterId = route.Cluster,
                Match = new RouteMatch { Path = route.Path },
                RateLimiterPolicy = route.RateLimiter,
            })
            .Concat(
                Hubs.Select(route => new RouteConfig
                {
                    RouteId = route.Route,
                    ClusterId = route.Cluster,
                    Match = new RouteMatch { Path = route.Path },
                })
            )
            .ToArray();

        return (routes, clusters);
    }
}
