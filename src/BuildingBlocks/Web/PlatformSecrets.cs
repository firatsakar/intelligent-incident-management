using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Web;

/// <summary>
/// The secrets an installation generated for itself on first start: one file per setting,
/// named as its environment variable would be (<c>Jwt__SigningKey</c>,
/// <c>ConnectionStrings__IncidentDb</c>), in the directory <c>IIM_SECRETS_DIR</c> names.
/// </summary>
/// <remarks>
/// Read only when that variable is set, which docker-compose.yml does; development keeps its user
/// secrets. Added after the environment, so a generated value is the one that counts — the files
/// and the databases they unlock were made together and have to stay together.
/// </remarks>
public static class PlatformSecrets
{
    public const string DirectoryVariable = "IIM_SECRETS_DIR";

    public static IConfigurationBuilder AddPlatformSecrets(this IConfigurationBuilder configuration)
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);

        return string.IsNullOrWhiteSpace(directory)
            ? configuration
            : configuration.AddKeyPerFile(directory, optional: false, reloadOnChange: false);
    }
}
