using System.Text.Json;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NotificationService.Infrastructure.Persistence;

/// <summary>
/// Encrypts the credentials of integrations saved before Adım 30, once, on start. Reading them
/// already works — a value without the encryption prefix is read as it is — but "readable" is not
/// "at rest encrypted", and a row nobody edits again would otherwise stay in the clear forever.
/// </summary>
public static class StoredSecretsEncryption
{
    public static async Task EncryptPlaintextAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            // The raw column, because the mapped property is decrypted on the way in and cannot
            // say whether what is stored was encrypted.
            var stored = await context
                .Database.SqlQueryRaw<StoredConfig>("SELECT \"Id\", config::text AS \"Config\" FROM integrations")
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

            var integrations = await context
                .Integrations.IgnoreQueryFilters()
                .Where(x => plain.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var integration in integrations)
                context.Entry(integration).Property("_config").IsModified = true;

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Encrypted the stored credentials of {Count} integration(s).", integrations.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not a reason to refuse to start: the rows stay readable, and the next start tries again.
            logger.LogWarning(ex, "Could not encrypt stored integration credentials; will retry on the next start.");
        }
    }

    private sealed class StoredConfig
    {
        public Guid Id { get; set; }

        public string Config { get; set; } = "{}";
    }
}
