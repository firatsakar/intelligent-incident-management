using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests;

// The filter between "an incident was analysed" and "this particular customer hears about it".
// Both filters are optional, and what they do when they have nothing to work with matters more
// than what they do when they do.
public sealed class IntegrationMatchesTests
{
    private static Integration Given(
        IncidentPriority? minPriority = null,
        string? categoryFilter = null
    ) =>
        Integration.Create(
            "ops email",
            NotificationChannelType.Email,
            new Dictionary<string, string>(),
            minPriority,
            categoryFilter
        );

    public sealed class PriorityFilter
    {
        [Theory]
        // MinPriority is inclusive and compares by severity, so High also matches Critical.
        [InlineData(IncidentPriority.Critical, true)]
        [InlineData(IncidentPriority.High, true)]
        [InlineData(IncidentPriority.Medium, false)]
        [InlineData(IncidentPriority.Low, false)]
        public void MinPriorityMatchesAnythingAtLeastAsSevere(
            IncidentPriority priority,
            bool expected
        )
        {
            var integration = Given(minPriority: IncidentPriority.High);

            Assert.Equal(expected, integration.Matches(priority, "Application"));
        }

        [Fact]
        public void NoMinPriorityMatchesEverything()
        {
            var integration = Given();

            Assert.True(integration.Matches(IncidentPriority.Low, "Application"));
            Assert.True(integration.Matches(IncidentPriority.Critical, "Application"));
        }

        [Fact]
        public void AnUnknownPrioritySkipsTheFilterRatherThanFailingIt()
        {
            // A null priority means the analysis produced something unrecognisable. Sending a
            // notification the filter might have excluded is a smaller failure than silently
            // dropping one for an incident nobody hears about.
            var integration = Given(minPriority: IncidentPriority.Critical);

            Assert.True(integration.Matches(priority: null, "Application"));
        }
    }

    public sealed class CategoryFilter
    {
        [Fact]
        public void MatchesTheNamedCategory()
        {
            Assert.True(Given(categoryFilter: "Database").Matches(null, "Database"));
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            // The category comes from an AI analysis, so its casing is not something we control.
            Assert.True(Given(categoryFilter: "Database").Matches(null, "database"));
        }

        [Fact]
        public void RejectsAnotherCategory()
        {
            Assert.False(Given(categoryFilter: "Database").Matches(null, "Application"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AnAbsentFilterMatchesEverything(string? categoryFilter)
        {
            Assert.True(Given(categoryFilter: categoryFilter).Matches(null, "Anything"));
        }
    }

    [Fact]
    public void BothFiltersMustPass()
    {
        var integration = Given(IncidentPriority.High, "Database");

        Assert.True(integration.Matches(IncidentPriority.Critical, "Database"));
        Assert.False(integration.Matches(IncidentPriority.Low, "Database"));
        Assert.False(integration.Matches(IncidentPriority.Critical, "Application"));
    }
}
