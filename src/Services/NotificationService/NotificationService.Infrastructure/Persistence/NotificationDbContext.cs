using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Infrastructure.Persistence;

public sealed class NotificationDbContext : DbContext
{
    private readonly IOrganizationContext _organization;

    public NotificationDbContext(
        DbContextOptions<NotificationDbContext> options,
        IOrganizationContext organization
    )
        : base(options)
    {
        _organization = organization;
    }

    // Read per query rather than captured at construction; TelemetryDbContext has the reason.
    private Guid ScopedOrganizationId => _organization.OrganizationId ?? Guid.Empty;

    public DbSet<Integration> Integrations => Set<Integration>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);

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
