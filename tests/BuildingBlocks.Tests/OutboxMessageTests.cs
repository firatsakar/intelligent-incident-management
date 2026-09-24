using BuildingBlocks.Outbox;

namespace BuildingBlocks.Tests;

public sealed class OutboxMessageTests
{
    [Fact]
    public void NewMessage_IsPendingAndUnretried()
    {
        var message = new OutboxMessage
        {
            Type = "SignalPromotedDomainEvent",
            Payload = "{}",
            OrganizationId = Guid.NewGuid(),
        };

        // The dispatcher's WHERE filter is "ProcessedOn is null". A default that was anything
        // else would make every new message invisible to it.
        Assert.Null(message.ProcessedOn);
        Assert.Null(message.Error);
        Assert.Equal(0, message.RetryCount);
    }
}
