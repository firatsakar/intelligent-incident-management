using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence;

public sealed class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options)
        : base(options) { }

    public DbSet<TelemetrySource> TelemetrySources => Set<TelemetrySource>();

    public DbSet<SourceCursor> SourceCursors => Set<SourceCursor>();

    public DbSet<LogRecord> LogRecords => Set<LogRecord>();

    public DbSet<ErrorSignature> ErrorSignatures => Set<ErrorSignature>();

    public DbSet<Signal> Signals => Set<Signal>();

    public DbSet<DetectionRule> DetectionRules => Set<DetectionRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TelemetryDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
