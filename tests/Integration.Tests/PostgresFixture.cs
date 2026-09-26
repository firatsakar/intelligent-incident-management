using BuildingBlocks.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Integration.Tests;

/// <summary>
/// One Postgres for the whole run (Adım 23), the same major version the platform runs on, and an
/// empty database of its own for every test that asks — so no test sees another's rows and none
/// depends on the order they run in.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> NewDatabaseAsync()
    {
        var name = "t_" + Guid.NewGuid().ToString("N")[..16];

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await create.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>
/// A service's own infrastructure registrations — its DbContext, repositories, interceptors —
/// pointed at a real database. Built the way the service builds them, so a mapping that only
/// works in the test's imagination does not pass.
/// </summary>
public static class ServiceUnderTest
{
    public static ServiceProvider Build(
        string connectionName,
        string connectionString,
        Action<IServiceCollection, IConfiguration> addInfrastructure
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"ConnectionStrings:{connectionName}"] = connectionString,
                    ["Database:MigrateOnStartup"] = "true",
                }
            )
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);

        addInfrastructure(services, configuration);

        return services.BuildServiceProvider();
    }

    /// <summary>A scope working on behalf of one organisation, as a request or a message would be.</summary>
    public static AsyncServiceScope ScopeFor(this ServiceProvider provider, Guid organizationId)
    {
        var scope = provider.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<IOrganizationContext>().Set(organizationId);

        return scope;
    }
}
