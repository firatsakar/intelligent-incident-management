using BuildingBlocks.Outbox;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence;

public sealed class TelemetryDbContext : DbContext
{
    private readonly IOrganizationContext _organization;

    public TelemetryDbContext(
        DbContextOptions<TelemetryDbContext> options,
        IOrganizationContext organization
    )
        : base(options)
    {
        _organization = organization;
    }

    /// <summary>
    /// The organisation every filter below compares against, read when a query runs rather than
    /// when this context was built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It was captured in the constructor at first, and that was a bug waiting for the right
    /// caller. A context is built when whatever depends on it is built, and a controller that
    /// takes its dependencies in its constructor builds the context before its action has had
    /// the chance to establish a scope — so the filter compared against nothing, found nothing,
    /// and the demo seeder's "already seeded?" check answered no every time. EF evaluates a member
    /// of the context in a query filter per query, so reading through to the scope here is what
    /// makes the answer current.
    /// </para>
    /// <para>
    /// An empty guid when there is no scope, and that is the safe direction: an unscoped context
    /// then matches nothing instead of matching everybody. A background sweep that reads no rows
    /// is a bug someone notices; one that reads every organisation's rows is not.
    /// </para>
    /// </remarks>
    private Guid ScopedOrganizationId => _organization.OrganizationId ?? Guid.Empty;

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
        modelBuilder.Entity<TelemetrySource>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);
        modelBuilder.Entity<SourceCursor>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);
        modelBuilder.Entity<LogRecord>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);
        modelBuilder.Entity<ErrorSignature>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);
        modelBuilder.Entity<Signal>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);
        modelBuilder.Entity<DetectionRule>().HasQueryFilter(x => x.OrganizationId == ScopedOrganizationId);

        base.OnModelCreating(modelBuilder);
    }
}
