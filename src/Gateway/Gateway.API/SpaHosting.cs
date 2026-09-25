using Microsoft.Extensions.FileProviders;

namespace Gateway.API;

/// <summary>
/// Serving the built console from the gateway, when there is one to serve.
/// </summary>
/// <remarks>
/// <para>
/// Until the gateway existed there was nowhere for <c>npm run build</c>'s output to be served
/// from: <c>vite.config.ts</c> defines <c>server.proxy</c> and nothing else, so the console could
/// only ever run behind a dev server. It could be developed and not deployed.
/// </para>
/// <para>
/// An explicit file provider rather than the host's web root. Assigning
/// <c>builder.Environment.WebRootPath</c> after <c>CreateBuilder</c> looks like it should work and
/// does not — the file provider is already built by then, and every request quietly 404s. Naming
/// the provider makes where the files come from a visible decision rather than a host convention
/// that can be wrong without saying so.
/// </para>
/// </remarks>
internal sealed class SpaHosting
{
    public const string RootConfigurationKey = "Spa:Root";

    private SpaHosting(string root, IFileProvider files)
    {
        Root = root;
        Files = files;
    }

    public string Root { get; }

    public IFileProvider Files { get; }

    /// <summary>
    /// The configured console, or null when there is none. A path that is configured but missing
    /// counts as none rather than as a failure: the gateway's job is routing, and a wrong path
    /// should not stop the API from answering — it should say so in the log and carry on.
    /// </summary>
    public static SpaHosting? TryResolve(IConfiguration configuration, ILogger logger)
    {
        var configured = configuration[RootConfigurationKey];

        if (string.IsNullOrWhiteSpace(configured))
        {
            logger.LogInformation(
                "No console configured ({Key} is unset); the gateway is serving the API only.",
                RootConfigurationKey
            );

            return null;
        }

        var root = Path.GetFullPath(configured);

        if (!File.Exists(Path.Combine(root, "index.html")))
        {
            logger.LogWarning(
                "{Key} is set to {Root} but there is no index.html there; serving the API only.",
                RootConfigurationKey,
                root
            );

            return null;
        }

        logger.LogInformation("Serving the console from {Root}.", root);

        return new SpaHosting(root, new PhysicalFileProvider(root));
    }
}
