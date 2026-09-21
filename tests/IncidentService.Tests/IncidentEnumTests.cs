using IncidentService.Domain.Enums;

namespace IncidentService.Tests;

public sealed class IncidentEnumTests
{
    [Fact]
    public void IncidentPriority_MatchesTheLocalCopyInNotificationService()
    {
        // NotificationService keeps its own copy of this enum because two service domains must
        // not reference each other, and IncidentAnalyzedEvent carries the priority as a string.
        // The two only stay interchangeable while the names match, and nothing else enforces it.
        Assert.Equal(
            ["Critical", "High", "Medium", "Low"],
            Enum.GetNames<IncidentPriority>()
        );
    }

    [Fact]
    public void IncidentSource_CoversBothWaysAnIncidentCanBeOpened()
    {
        // Manual creation and telemetry promotion are the two entry points today.
        Assert.Contains(IncidentSource.Manual, Enum.GetValues<IncidentSource>());
        Assert.Contains(IncidentSource.Telemetry, Enum.GetValues<IncidentSource>());
    }
}
