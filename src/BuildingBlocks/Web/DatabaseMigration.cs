using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web;

/// <summary>
/// Brings a service's own database up to date as it starts, when configured to (Adım 26).
/// </summary>
/// <remarks>
/// <para>
/// An installation started with <c>docker compose</c> has nobody at hand to run
/// <c>dotnet ef database update</c>, so each service applies its own migrations before anything
/// that reads the schema starts: the seeders and the outbox dispatchers are hosted services, and
/// hosted services start after this returns.
/// </para>
/// <para>
/// Off unless <see cref="ConfigurationKey"/> is true. In development migrations are applied by
/// hand, after they have been read — a migration that surprises is better met at a prompt than
/// found already applied.
/// </para>
/// </remarks>
public static class DatabaseMigration
{
    public const string ConfigurationKey = "Database:MigrateOnStartup";

    public static async Task MigrateOnStartupAsync<TContext>(
        this IHost host,
        CancellationToken cancellationToken = default
    )
        where TContext : DbContext
    {
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        if (!configuration.GetValue<bool>(ConfigurationKey))
            return;

        await using var scope = host.Services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigration));

        var pending = await PendingAsync(context, cancellationToken);

        if (pending.Count == 0)
        {
            logger.LogInformation("Database for {Context} is up to date.", typeof(TContext).Name);
            return;
        }

        logger.LogInformation(
            "Applying {Count} migration(s) to the database for {Context}: {Migrations}",
            pending.Count,
            typeof(TContext).Name,
            string.Join(", ", pending)
        );

        // A failure here is left to stop the process: a service running on a schema it does not
        // match fails later and less clearly, and the container's restart policy tries again.
        await context.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<List<string>> PendingAsync(DbContext context, CancellationToken cancellationToken)
    {
        // A database that does not exist yet is created by MigrateAsync, and everything is pending.
        if (!await context.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken))
            return context.Database.GetMigrations().ToList();

        // Npgsql answers "does the history table exist?" by selecting from it and catching the
        // failure, and EF's command logger prints that failure as an ERR — twice, since
        // MigrateAsync asks again. Nothing is wrong, but it would be the first thing every new
        // installation shows. CREATE TABLE IF NOT EXISTS settles the question without an error,
        // and MigrateAsync then finds the table too.
        var history = context.GetService<IHistoryRepository>();
        await history.CreateIfNotExistsAsync(cancellationToken);

        var applied = (await history.GetAppliedMigrationsAsync(cancellationToken))
            .Select(row => row.MigrationId)
            .ToHashSet();

        return context.Database.GetMigrations().Where(id => !applied.Contains(id)).ToList();
    }
}
