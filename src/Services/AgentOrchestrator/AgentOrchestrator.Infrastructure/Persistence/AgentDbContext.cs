using AgentOrchestrator.Domain.Aggregates;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AgentOrchestrator.Infrastructure.Persistence;

public sealed class AgentDbContext : DbContext
{
    public AgentDbContext(DbContextOptions<AgentDbContext> options)
        : base(options) { }

    public DbSet<IncidentAnalysis> Analyses => Set<IncidentAnalysis>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);

        // The outbox mapping lives in BuildingBlocks now, so it is not picked up by the assembly
        // scan above and has to be applied explicitly.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
