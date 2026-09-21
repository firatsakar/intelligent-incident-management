using NotificationService.Domain.Enums;

namespace NotificationService.Tests;

public sealed class ChannelEnumTests
{
    [Fact]
    public void NotificationChannelType_HasExactlyTheChannelsThatAreRegistered()
    {
        // Channels are resolved through keyed DI on this enum. A new member added without a
        // matching registration fails at dispatch time, on a real incident, rather than at
        // startup — so the set is pinned here.
        Assert.Equal(
            [
                NotificationChannelType.Email,
                NotificationChannelType.Webhook,
                NotificationChannelType.Jira,
            ],
            Enum.GetValues<NotificationChannelType>()
        );
    }

    [Fact]
    public void IncidentPriority_DeclarationOrderIsSeverityOrder()
    {
        // Integration.Matches compares priorities with <=, which only means "at least as severe"
        // while Critical holds the lowest ordinal. Reordering this enum inverts every filter.
        Assert.True(IncidentPriority.Critical < IncidentPriority.High);
        Assert.True(IncidentPriority.High < IncidentPriority.Medium);
        Assert.True(IncidentPriority.Medium < IncidentPriority.Low);
    }
}
