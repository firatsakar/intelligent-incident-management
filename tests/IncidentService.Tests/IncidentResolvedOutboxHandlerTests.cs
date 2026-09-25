using System.Text.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.Outbox;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Events;
using IncidentService.Infrastructure.Outbox;
using NSubstitute;

namespace IncidentService.Tests;

// The row the interceptor wrote becomes the message telemetry learns from. What has to survive
// the trip: whose incident it was (from the row, not from any scope), a stable event id across
// retries, and the verdict spelled the way the consumer reads it.
public sealed class IncidentResolvedOutboxHandlerTests
{
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();

    [Fact]
    public async Task PublishesTheResolutionWithTheRowsIdAndOrganisation()
    {
        var domainEvent = new IncidentResolvedDomainEvent
        {
            IncidentId = Guid.NewGuid(),
            Status = IncidentStatus.Closed,
            Verdict = IncidentVerdict.FalsePositive,
            ResolvedAt = new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc),
        };

        // Serialised exactly as ConvertDomainEventsToOutboxInterceptor does it.
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(IncidentResolvedDomainEvent),
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredOn = DateTimeOffset.UtcNow,
            OrganizationId = Guid.NewGuid(),
        };

        var handler = new IncidentResolvedOutboxHandler(_eventBus);

        Assert.Equal(message.Type, handler.MessageType);

        await handler.HandleAsync(message);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<IncidentResolvedEvent>(published =>
                published.Id == message.Id
                && published.OrganizationId == message.OrganizationId
                && published.IncidentId == domainEvent.IncidentId
                && published.Status == "Closed"
                && published.Verdict == "FalsePositive"
                && published.ResolvedAt == domainEvent.ResolvedAt
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
