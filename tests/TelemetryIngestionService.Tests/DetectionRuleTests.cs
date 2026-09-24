using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Tests;

public sealed class DetectionRuleTests
{
    // One organisation for the whole file. These are unit tests of rules, not of scoping — the
    // filters that make the column matter live in the DbContext — so the value only has to be
    // consistent.
    private static readonly Guid Organization = Guid.NewGuid();
    private static DetectionRule Create(string? service) =>
        DetectionRule.Create(
            Organization,
            "burst",
            service,
            LogSeverity.Error,
            windowSeconds: 300,
            threshold: 3,
            dedupWindowHours: 24,
            promoteThreshold: 0.90
        );

    [Theory]
    [InlineData("checkout-service")]
    [InlineData("billing-service")]
    public void ANullServiceIsTheCatchAll(string service)
    {
        Assert.True(Create(service: null).AppliesTo(service));
    }

    [Theory]
    [InlineData("checkout-service", true)]
    // Service names come from log payloads, where casing is not something we control.
    [InlineData("Checkout-Service", true)]
    [InlineData("billing-service", false)]
    public void ANamedServiceMatchesCaseInsensitively(string service, bool expected)
    {
        Assert.Equal(expected, Create("checkout-service").AppliesTo(service));
    }

    [Fact]
    public void WindowsAreExposedAsTimeSpans()
    {
        var rule = Create(service: null);

        Assert.Equal(TimeSpan.FromMinutes(5), rule.Window);
        Assert.Equal(TimeSpan.FromHours(24), rule.DedupWindow);
    }

    [Fact]
    public void UpdateChangesTheTuningButNotTheService()
    {
        // Thresholds live in the database so they can be tuned under pressure without a deploy.
        // The service a rule is scoped to is its identity, not its tuning, and Update leaves it.
        var rule = Create("checkout-service");

        rule.Update(
            "burst (tightened)",
            LogSeverity.Fatal,
            windowSeconds: 60,
            threshold: 10,
            dedupWindowHours: 1,
            promoteThreshold: 0.95
        );

        Assert.Equal("burst (tightened)", rule.Name);
        Assert.Equal(LogSeverity.Fatal, rule.MinSeverity);
        Assert.Equal(TimeSpan.FromMinutes(1), rule.Window);
        Assert.Equal(10, rule.Threshold);
        Assert.Equal(TimeSpan.FromHours(1), rule.DedupWindow);
        Assert.Equal(0.95, rule.PromoteThreshold);
        Assert.Equal("checkout-service", rule.Service);
    }

    [Fact]
    public void EnableAndDisable()
    {
        var rule = Create(service: null);
        Assert.True(rule.IsEnabled);

        rule.Disable();
        Assert.False(rule.IsEnabled);

        rule.Enable();
        Assert.True(rule.IsEnabled);
    }
}
