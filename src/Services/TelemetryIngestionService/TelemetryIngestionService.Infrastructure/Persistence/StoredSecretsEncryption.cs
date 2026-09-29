using System.Text.Json;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TelemetryIngestionService.Infrastructure.Persistence;

/// <summary>
/// Encrypts the credentials of telemetry sources saved before encryption at rest, once, on start. Reading them
/// already works — a value without the encryption prefix is read as it is — but a source nobody
/// edits again would otherwise keep its API key in the clear forever.
/// </summary>
public static class StoredSecretsEncryption
{
    public static async Task EncryptPlaintextAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            // The raw column, because the mapped property is decrypted on the way in and cannot
            // say whether what is stored was encrypted.
            var stored = await context
                .Database.SqlQueryRaw<StoredConfig>("SELECT \"Id\", config::text AS \"Config\" FROM telemetry_sources")
                .ToListAsync(cancellationToken);

            var plain = stored
                .Where(row =>
                    ConfigSecrets.HasPlaintextSecret(
                        JsonSerializer.Deserialize<Dictionary<string, string>>(row.Config) ?? []
                    )
                )
                .Select(row => row.Id)
                .ToList();

            if (plain.Count == 0)
                return;

            var sources = await context
                .TelemetrySources.IgnoreQueryFilters()
                .Where(x => plain.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var source in sources)
                context.Entry(source).Property("_config").IsModified = true;

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Encrypted the stored credentials of {Count} telemetry source(s).", sources.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not a reason to refuse to start: the rows stay readable, and the next start tries again.
            logger.LogWarning(ex, "Could not encrypt stored telemetry source credentials; will retry on the next start.");
        }
    }

    private sealed class StoredConfig
    {
        public Guid Id { get; set; }

        public string Config { get; set; } = "{}";
    }
}
