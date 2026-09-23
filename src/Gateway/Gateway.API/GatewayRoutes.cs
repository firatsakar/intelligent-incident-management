using Yarp.ReverseProxy.Configuration;

namespace Gateway.API;

/// <summary>
/// The path to service map, and the only place it exists.
/// </summary>
/// <remarks>
/// It lived in <c>web/vite.config.ts</c> until now, which meant the browser could only reach the
/// four services while a dev server was running: Vite defines <c>server.proxy</c> and nothing
/// else, so a built <c>dist/</c> had no way to call anything. This file is the same nine entries,
/// moved to where they work in development and in production alike.
///
/// In memory rather than in configuration on purpose. A route table is a structure with rules —
/// which prefixes belong to the same service, which ones carry a WebSocket — and those rules are
/// worth stating in a language that can hold a comment. The destination addresses are the part
/// that genuinely varies per environment, so those alone are read from configuration, the way
/// <see cref="BuildingBlocks.Observability.LoggingExtensions"/> reads the Seq URL.
/// </remarks>
internal static class GatewayRoutes
{
    /// <summary>Configuration section holding per-environment service addresses.</summary>
    public const string ServicesConfigurationSection = "Services";

    // One cluster per service, not per prefix: two of the four answer on more than one prefix and
    // a cluster is a destination, not a route.
    private const string IncidentCluster = "incident";
    private const string AgentCluster = "agent";
    private const string NotificationCluster = "notification";
    private const string TelemetryCluster = "telemetry";

    private static readonly (string Cluster, string ConfigurationKey, string Fallback)[] Services =
    [
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
    // swallow `/api/telemetry-sources`. Two entries, no precedence rule to remember.
    private static readonly (string Route, string Path, string Cluster)[] Api =
    [
        ("incidents", "/api/incidents/{**rest}", IncidentCluster),
        // Routed because the endpoint exists and is reachable; the console never calls it. The
        // normal trigger for an analysis is IncidentDetectedEvent over RabbitMQ.
        ("analyses", "/api/analyses/{**rest}", AgentCluster),
        ("notifications", "/api/notifications/{**rest}", NotificationCluster),
        ("integrations", "/api/integrations/{**rest}", NotificationCluster),
        ("telemetry", "/api/telemetry/{**rest}", TelemetryCluster),
        ("telemetry-sources", "/api/telemetry-sources/{**rest}", TelemetryCluster),
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

        var routes = Api.Concat(Hubs)
            .Select(route => new RouteConfig
            {
                RouteId = route.Route,
                ClusterId = route.Cluster,
                Match = new RouteMatch { Path = route.Path },
            })
            .ToArray();

        return (routes, clusters);
    }
}
