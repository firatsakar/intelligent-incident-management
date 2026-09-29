using BuildingBlocks.Outbox;
using IdentityService.Infrastructure.Persistence;

namespace IdentityService.Infrastructure.Outbox;

/// <summary>The outbox over this service's own context; the claiming lives in <see cref="EfOutboxStore{TContext}"/>.</summary>
public sealed class IdentityOutboxStore(IdentityDbContext context) : EfOutboxStore<IdentityDbContext>(context);
