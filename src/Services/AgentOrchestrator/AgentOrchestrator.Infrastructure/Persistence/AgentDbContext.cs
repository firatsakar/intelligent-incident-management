using BuildingBlocks.SharedKernel;
using AgentOrchestrator.Domain.Aggregates;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AgentOrchestrator.Infrastructure.Persistence;

public sealed class AgentDbContext : DbContext
{
    private readonly IOrganizationContext _organization;

    public AgentDbContext(DbContextOptions<AgentDbContext> options, IOrganizationContext organization)
        : base(options)
    {
        _organization = organization;
    }

    // Read per query rather than captured at construction; TelemetryDbContext has the reason.
    private Guid ScopedOrganizationId => _organization.OrganizationId ?? Guid.Empty;

    public DbSet<IncidentAnalysis> Analyses => Set<IncidentAnalysis>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<GitHubConnection> GitHubConnections => Set<GitHubConnection>();

    public DbSet<AiSettings> AiSettings => Set<AiSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);

        // The outbox mapping lives in BuildingBlocks now, so it is not picked up by the assembly
        // scan above and has to be applied explicitly.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        modelBuilder
            .Entity<IncidentAnalysis>()
            .HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);

        // An organisation's GitHub token is the most sensitive thing this service holds; another
        // organisation's scope resolves to no connection at all.
        modelBuilder
            .Entity<GitHubConnection>()
            .HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);

        modelBuilder.Entity<AiSettings>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);

        base.OnModelCreating(modelBuilder);
    }
}
