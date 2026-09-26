using BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BuildingBlocks.Tests;

// Adım 26: an installation applies its migrations as it starts; development does not, and runs
// them by hand after reading them. The flag is the whole difference, so the flag is what is pinned.
public sealed class DatabaseMigrationTests
{
    private sealed class Probe(DbContextOptions<Probe> options) : DbContext(options);

    private static IHost Host(string? flag, bool withDatabase)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseMigration.ConfigurationKey] = flag })
                .Build()
        );
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        if (withDatabase)
            services.AddDbContext<Probe>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var host = Substitute.For<IHost>();
        host.Services.Returns(services.BuildServiceProvider());

        return host;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task OffItDoesNotEvenAskForTheDatabase(string? flag)
    {
        // No DbContext is registered: resolving one would throw.
        await Host(flag, withDatabase: false).MigrateOnStartupAsync<Probe>();
    }

    [Fact]
    public async Task OnItMigratesTheServicesOwnDatabase()
    {
        // The in-memory provider is not relational and says so as soon as it is asked about its
        // database — which is the proof that, with the flag on, the service's own context was.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Host("true", withDatabase: true).MigrateOnStartupAsync<Probe>()
        );

        Assert.Contains("relational", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
