using BuildingBlocks.SharedKernel;
using AgentOrchestrator.Domain.Aggregates;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AgentOrchestrator.Infrastructure.Persistence;

public sealed class AgentDbContext : DbContext
{
    private readonly IOrganizationContext _organization;
    private readonly SecretProtector _secrets;

    public AgentDbContext(
        DbContextOptions<AgentDbContext> options,
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

    public DbSet<IncidentAnalysis> Analyses => Set<IncidentAnalysis>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<GitHubConnection> GitHubConnections => Set<GitHubConnection>();

    public DbSet<AiSettings> AiSettings => Set<AiSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);

        // Adım 30: the organisation's GitHub token is encrypted in the column. A local rather than
        // the field, so the cached model does not hold this context.
        var secrets = _secrets;
        modelBuilder
            .Entity<GitHubConnection>()
            .Property(x => x.Token)
            .HasConversion(value => secrets.Protect(value), stored => secrets.Unprotect(stored));

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
