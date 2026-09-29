using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Infrastructure.Persistence;

public sealed class NotificationDbContext : DbContext
{
    private readonly IOrganizationContext _organization;
    private readonly SecretProtector _secrets;

    public NotificationDbContext(
        DbContextOptions<NotificationDbContext> options,
        IOrganizationContext organization,
        SecretProtector secrets
    )
        : base(options)
    {
        _organization = organization;
        _secrets = secrets;
    }

    // Read per query rather than captured at construction; TelemetryDbContext has the reason.
    private Guid ScopedOrganizationId => _organization.OrganizationId ?? Guid.Empty;

    public DbSet<Integration> Integrations => Set<Integration>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);

        // Adım 30: the credentials in an integration's config (an SMTP password, a Jira token) are
        // encrypted in the column and decrypted on the way back, so the aggregate never sees the
        // difference. A local rather than the field, so the cached model does not hold this context.
        var secrets = _secrets;
        var config = modelBuilder.Entity<Integration>().Property<Dictionary<string, string>>("_config");
        config.HasConversion(
            new ValueConverter<Dictionary<string, string>, string>(
                value => JsonSerializer.Serialize(ConfigSecrets.Protect(value, secrets), (JsonSerializerOptions?)null),
                json =>
                    ConfigSecrets.Unprotect(
                        JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null)
                            ?? new Dictionary<string, string>(),
                        secrets
                    )
            ),
            config.Metadata.GetValueComparer()
        );

        // The dispatcher runs in the scope the bus took from IncidentAnalyzedEvent, so the
        // integrations it can see are exactly the ones belonging to the organisation whose
        // incident was analysed. That is the whole fix for cross-organisation delivery; the
        // handler did not have to change to get it.
        modelBuilder.Entity<Integration>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);
        modelBuilder
            .Entity<NotificationDelivery>()
            .HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);

        base.OnModelCreating(modelBuilder);
    }
}
