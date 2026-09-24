using BuildingBlocks.Outbox;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence;

public sealed class TelemetryDbContext : DbContext
{
    /// <summary>
    /// Captured once per context, which is once per scope, which is where the organisation was
    /// established — by the HTTP middleware, by the bus, or by the polling loop reading a source.
    /// </summary>
    /// <remarks>
    /// An empty guid when there is no scope, and that is the safe direction: an unscoped context
    /// then matches nothing instead of matching everybody. A background sweep that reads no rows
    /// is a bug someone notices; one that reads every organisation's rows is not.
    /// </remarks>
    private readonly Guid _organizationId;

    public TelemetryDbContext(
        DbContextOptions<TelemetryDbContext> options,
        IOrganizationContext organization
    )
        : base(options)
    {
        _organizationId = organization.OrganizationId ?? Guid.Empty;
    }

    public DbSet<TelemetrySource> TelemetrySources => Set<TelemetrySource>();

    public DbSet<SourceCursor> SourceCursors => Set<SourceCursor>();

    public DbSet<LogRecord> LogRecords => Set<LogRecord>();

    public DbSet<ErrorSignature> ErrorSignatures => Set<ErrorSignature>();

    public DbSet<Signal> Signals => Set<Signal>();

    public DbSet<DetectionRule> DetectionRules => Set<DetectionRule>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TelemetryDbContext).Assembly);

        // The outbox mapping lives in BuildingBlocks, so the assembly scan above does not see it.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // Every table in this service except the outbox, which is infrastructure rather than
        // anybody's data — its rows carry an organisation so the message can, and the dispatcher
        // that reads them serves all of them.
        //
        // Stated one entity at a time rather than swept over the model: a filter added by
        // reflection is a filter nobody can see at the point where it matters, and the whole
        // argument for query filters is that forgetting one must not be possible. Forgetting one
        // here is visible in this list.
        modelBuilder.Entity<TelemetrySource>().HasQueryFilter(x => x.OrganizationId == _organizationId);
        modelBuilder.Entity<SourceCursor>().HasQueryFilter(x => x.OrganizationId == _organizationId);
        modelBuilder.Entity<LogRecord>().HasQueryFilter(x => x.OrganizationId == _organizationId);
        modelBuilder.Entity<ErrorSignature>().HasQueryFilter(x => x.OrganizationId == _organizationId);
        modelBuilder.Entity<Signal>().HasQueryFilter(x => x.OrganizationId == _organizationId);
        modelBuilder.Entity<DetectionRule>().HasQueryFilter(x => x.OrganizationId == _organizationId);

        base.OnModelCreating(modelBuilder);
    }
}
