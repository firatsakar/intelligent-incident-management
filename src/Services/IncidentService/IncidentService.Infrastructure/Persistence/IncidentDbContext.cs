using IncidentService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IncidentService.Infrastructure.Persistence;

public sealed class IncidentDbContext : DbContext
{
    public IncidentDbContext(DbContextOptions<IncidentDbContext> options)
        : base(options) { }

    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IncidentDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}