using System.Diagnostics;
using System.Text.Json;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocks.Outbox;

// Atomicity comes from the transaction, not from cleverness: domain events are harvested into
// outbox rows inside the same SaveChanges that writes the aggregate, so a message can never exist
// without the state change that caused it, nor the other way round.
public sealed class ConvertDomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    private readonly IOrganizationContext _organization;

    public ConvertDomainEventsToOutboxInterceptor(IOrganizationContext organization)
    {
        _organization = organization;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        var context = eventData.Context;

        if (context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        // Whatever is in flight as the aggregate changes — the request, the consumed message, the
        // poll. Only W3C ids travel: the hierarchical format has no traceparent to hand on.
        var traceParent = Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity
            ? activity.Id
            : null;

        var outboxMessages = context
            .ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .SelectMany(aggregate =>
            {
                var events = aggregate.DomainEvents.ToList();
                aggregate.ClearDomainEvents();
                return events;
            })
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = domainEvent.GetType().Name,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                OccurredOn = DateTimeOffset.UtcNow,

                // Required, and refusing here is the point: a row written without an owner would
                // become a message published to everyone. The scope is established by whoever
                // opened it — the HTTP middleware, the bus, or the poller reading its source.
                OrganizationId = _organization.Required,
                TraceParent = traceParent,
            })
            .ToList();

        if (outboxMessages.Count > 0)
        {
            context.Set<OutboxMessage>().AddRange(outboxMessages);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
