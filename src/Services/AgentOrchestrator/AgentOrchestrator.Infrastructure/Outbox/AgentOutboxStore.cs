using BuildingBlocks.Outbox;
using AgentOrchestrator.Infrastructure.Persistence;

namespace AgentOrchestrator.Infrastructure.Outbox;

/// <summary>The outbox over this service's own context; the claiming lives in <see cref="EfOutboxStore{TContext}"/>.</summary>
public sealed class AgentOutboxStore(AgentDbContext context) : EfOutboxStore<AgentDbContext>(context);
