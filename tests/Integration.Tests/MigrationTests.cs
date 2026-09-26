using AgentOrchestrator.Infrastructure.Persistence;
using BuildingBlocks.Web;
using IdentityService.Infrastructure.Persistence;
using IncidentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.Infrastructure.Persistence;
using NSubstitute;
using TelemetryIngestionService.Infrastructure.Persistence;

namespace Integration.Tests;

// Every service's migrations, applied the way an installation applies them (Adım 26) to an empty
// database, and checked against the model the code actually has. A model change without its
// migration is found here rather than by the first service that starts on a new server.
[Collection(PostgresCollection.Name)]
public sealed class MigrationTests(PostgresFixture postgres)
{
    private async Task MigratesAndMatchesTheModel<TContext>(
        string connectionName,
        Action<Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration> addInfrastructure
    )
        where TContext : DbContext
    {
        await using var provider = ServiceUnderTest.Build(connectionName, await postgres.NewDatabaseAsync(), addInfrastructure);

        var host = Substitute.For<IHost>();
        host.Services.Returns(provider);

        await host.MigrateOnStartupAsync<TContext>();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges(), $"{typeof(TContext).Name} has model changes with no migration.");

        // A second start finds nothing to do and does not fail.
        await host.MigrateOnStartupAsync<TContext>();
    }

    [Fact]
    public Task Identity() =>
        MigratesAndMatchesTheModel<IdentityDbContext>(
            "IdentityDb",
            (services, configuration) => IdentityService.Infrastructure.ServiceCollectionExtensions.AddInfrastructure(services, configuration)
        );

    [Fact]
    public Task Incident() =>
        MigratesAndMatchesTheModel<IncidentDbContext>(
            "IncidentDb",
            (services, configuration) => IncidentService.Infrastructure.ServiceCollectionExtensions.AddInfrastructure(services, configuration)
        );

    [Fact]
    public Task Agent() =>
        MigratesAndMatchesTheModel<AgentDbContext>(
            "AgentDb",
            (services, configuration) => AgentOrchestrator.Infrastructure.ServiceCollectionExtensions.AddInfrastructure(services, configuration)
        );

    [Fact]
    public Task Notification() =>
        MigratesAndMatchesTheModel<NotificationDbContext>(
            "NotificationDb",
            (services, configuration) => NotificationService.Infrastructure.ServiceCollectionExtensions.AddInfrastructure(services, configuration)
        );

    [Fact]
    public Task Telemetry() =>
        MigratesAndMatchesTheModel<TelemetryDbContext>(
            "TelemetryDb",
            (services, configuration) => TelemetryIngestionService.Infrastructure.ServiceCollectionExtensions.AddInfrastructure(services, configuration)
        );
}
