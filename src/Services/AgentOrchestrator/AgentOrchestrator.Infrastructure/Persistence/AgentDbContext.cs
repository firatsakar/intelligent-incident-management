using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Infrastructure.Outbox;
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

        base.OnModelCreating(modelBuilder);
    }
}
