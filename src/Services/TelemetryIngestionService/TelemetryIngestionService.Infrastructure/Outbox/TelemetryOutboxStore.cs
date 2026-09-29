using BuildingBlocks.Outbox;
using TelemetryIngestionService.Infrastructure.Persistence;

namespace TelemetryIngestionService.Infrastructure.Outbox;

/// <summary>The outbox over this service's own context; the claiming lives in <see cref="EfOutboxStore{TContext}"/>.</summary>
public sealed class TelemetryOutboxStore(TelemetryDbContext context) : EfOutboxStore<TelemetryDbContext>(context);
