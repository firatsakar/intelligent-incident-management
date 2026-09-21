using System.Text.Json;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocks.Outbox;

// Atomicity comes from the transaction, not from cleverness: domain events are harvested into
// outbox rows inside the same SaveChanges that writes the aggregate, so a message can never exist
// without the state change that caused it, nor the other way round.
public sealed class ConvertDomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        var context = eventData.Context;

        if (context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

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
            })
            .ToList();

        if (outboxMessages.Count > 0)
        {
            context.Set<OutboxMessage>().AddRange(outboxMessages);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
