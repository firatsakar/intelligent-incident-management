using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Changes;
using AgentOrchestrator.Application.Commands.AnalyzeIncident;
using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.ValueObjects;
using BuildingBlocks.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgentOrchestrator.Tests;

// Whether an analysis may read code, and which code, is decided here — from the organisation's
// own connection and the incident's service — before the analyzer is ever called.
public sealed class AnalyzeIncidentCodeContextTests
{
    private static readonly Guid Organization = Guid.NewGuid();
    private static readonly DateTime Started = new(2026, 9, 25, 14, 20, 0, DateTimeKind.Utc);
    private static readonly RepositoryMapping Shop = new("checkout-service", "acme", "shop", null);

    private readonly IAiAnalyzer _analyzer = Substitute.For<IAiAnalyzer>();
    private readonly IIncidentAnalysisRepository _analyses = Substitute.For<IIncidentAnalysisRepository>();
    private readonly IGitHubConnectionRepository _connections = Substitute.For<IGitHubConnectionRepository>();
    private readonly AnalyzeIncidentCommandHandler _handler;

    private CodeContext? _received;

    public AnalyzeIncidentCodeContextTests()
    {
        var organization = new OrganizationContext();
        organization.Set(Organization);

        _handler = new AnalyzeIncidentCommandHandler(
            _analyzer,
            _analyses,
            NullLogger<AnalyzeIncidentCommandHandler>.Instance,
            organization,
            _connections
        );

        _analyzer
            .AnalyzeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Do<CodeContext?>(c => _received = c), Arg.Any<CancellationToken>())
            .Returns(new AnalysisResult { SuggestedPriority = "High", SuggestedCategory = "Application", Reasoning = "r" });
    }

    private Task Analyze(string? service) =>
        _handler.Handle(
            new AnalyzeIncidentCommand
            {
                IncidentId = Guid.NewGuid(),
                Title = "checkout-service: TimeoutException",
                Description = "d",
                Service = service,
                DetectedAt = Started,
            },
            CancellationToken.None
        );

    [Fact]
    public async Task AMappedServiceIsAnalysedWithItsRepositoryAndTheOrganisationsToken()
    {
        _connections.GetAsync(Arg.Any<CancellationToken>()).Returns(GitHubConnection.Create(Organization, "ghp_secret", [Shop], true));

        await Analyze("checkout-service");

        Assert.NotNull(_received);
        Assert.Equal(Shop, _received.Repository);
        Assert.Equal("ghp_secret", _received.Token);
        Assert.Equal(Started, _received.ProblemStartedAt);

        // Printed, the context names the repository and never the token.
        Assert.DoesNotContain("ghp_secret", _received.ToString());
    }

    [Fact]
    public async Task WithoutAConnectionOrAMappingThereIsNoCode()
    {
        await Analyze("checkout-service");
        Assert.Null(_received);

        _connections.GetAsync(Arg.Any<CancellationToken>()).Returns(GitHubConnection.Create(Organization, "ghp_secret", [Shop], true));

        await Analyze("search-service");
        Assert.Null(_received);

        await Analyze(null);
        Assert.Null(_received);
    }
}
