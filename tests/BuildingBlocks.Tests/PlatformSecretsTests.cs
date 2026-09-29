using BuildingBlocks.Web;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Tests;

// An installation generates its own secrets on first start and the services read them as
// configuration, one file per setting. Development, without the variable, reads none of it.
public sealed class PlatformSecretsTests
{
    [Fact]
    public void TheGeneratedFilesAreReadAsSettings()
    {
        var directory = Directory.CreateTempSubdirectory("iim-secrets-").FullName;

        try
        {
            File.WriteAllText(Path.Combine(directory, "Jwt__SigningKey"), "generated-signing-key");
            File.WriteAllText(Path.Combine(directory, "ConnectionStrings__IncidentDb"), "Host=postgres;Password=p");

            Environment.SetEnvironmentVariable(PlatformSecrets.DirectoryVariable, directory);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "from-environment" })
                .AddPlatformSecrets()
                .Build();

            // The generated value wins: it and the databases it unlocks were made together.
            Assert.Equal("generated-signing-key", configuration["Jwt:SigningKey"]);
            Assert.Equal("Host=postgres;Password=p", configuration.GetConnectionString("IncidentDb"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(PlatformSecrets.DirectoryVariable, null);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WithoutTheVariableNothingIsRead()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "user-secret" })
            .AddPlatformSecrets()
            .Build();

        Assert.Equal("user-secret", configuration["Jwt:SigningKey"]);
    }
}
