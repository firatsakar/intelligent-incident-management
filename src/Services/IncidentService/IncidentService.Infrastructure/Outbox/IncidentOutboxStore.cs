using BuildingBlocks.Outbox;
using IncidentService.Infrastructure.Persistence;

namespace IncidentService.Infrastructure.Outbox;

/// <summary>The outbox over this service's own context; the claiming lives in <see cref="EfOutboxStore{TContext}"/>.</summary>
public sealed class IncidentOutboxStore(IncidentDbContext context) : EfOutboxStore<IncidentDbContext>(context);
