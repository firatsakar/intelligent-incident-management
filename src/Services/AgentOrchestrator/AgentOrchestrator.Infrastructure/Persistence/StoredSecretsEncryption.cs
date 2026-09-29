using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Encrypts GitHub tokens saved before Adım 30, once, on start. Reading them already works — a
/// value without the encryption prefix is read as it is — but a connection nobody edits again would
/// otherwise keep its token in the clear forever.
/// </summary>
public static class StoredSecretsEncryption
{
    public static async Task EncryptPlaintextAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AgentDbContext>();

            // The raw column, because the mapped property is decrypted on the way in.
            var plain = await context
                .Database.SqlQueryRaw<Guid>(
                    "SELECT \"Id\" AS \"Value\" FROM github_connections WHERE \"Token\" <> '' AND \"Token\" NOT LIKE {0}",
                    SecretProtector.Prefix + "%"
                )
                .ToListAsync(cancellationToken);

            if (plain.Count == 0)
                return;

            var connections = await context
                .GitHubConnections.IgnoreQueryFilters()
                .Where(x => plain.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var connection in connections)
                context.Entry(connection).Property(x => x.Token).IsModified = true;

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Encrypted {Count} stored GitHub token(s).", connections.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not a reason to refuse to start: the rows stay readable, and the next start tries again.
            logger.LogWarning(ex, "Could not encrypt stored GitHub tokens; will retry on the next start.");
        }
    }
}
