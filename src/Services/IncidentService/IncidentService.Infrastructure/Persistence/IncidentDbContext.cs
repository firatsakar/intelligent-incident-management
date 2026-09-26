using BuildingBlocks.Outbox;
using BuildingBlocks.SharedKernel;
using IncidentService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IncidentService.Infrastructure.Persistence;

public sealed class IncidentDbContext : DbContext
{
    private readonly IOrganizationContext _organization;

    public IncidentDbContext(
        DbContextOptions<IncidentDbContext> options,
        IOrganizationContext organization
    )
        : base(options)
    {
        _organization = organization;
    }

    // Read per query, not captured at construction — a controller builds this context before its
    // action establishes a scope, and a captured value would be the empty one. Empty when there is
    // no scope, which matches nothing rather than everything. TelemetryDbContext has the history.
    private Guid ScopedOrganizationId => _organization.OrganizationId ?? Guid.Empty;

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<IncidentApiKey> IncidentApiKeys => Set<IncidentApiKey>();

    // Not filtered by organisation: the dispatcher reads every organisation's pending rows and
    // sets each one's scope from the row itself.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IncidentDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // An incident id from another organisation now resolves to nothing, which the query
        // handler already turns into a 404. That is the right answer rather than a 403: a
        // forbidden response would confirm the id exists, which is itself something one
        // organisation has no business learning about another.
        modelBuilder.Entity<Incident>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);
        modelBuilder.Entity<IncidentApiKey>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);

        base.OnModelCreating(modelBuilder);
    }
}
