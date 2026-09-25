using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Changes;
using AgentOrchestrator.Domain.ValueObjects;
using AgentOrchestrator.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AgentOrchestrator.Tests;

// What the model can reach with the two code tools, and what it can make the platform show.
public sealed class ChangeToolsTests
{
    private static readonly RepositoryMapping Shop = new("checkout-service", "acme", "shop", "main");
    private static readonly DateTime Started = new(2026, 9, 25, 14, 20, 0, DateTimeKind.Utc);

    private static readonly RecentChange Timeout = new(
        "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
        "Reduce payment gateway timeout to 500ms",
        "ayse",
        Started.AddMinutes(-15)
    );

    private readonly IRepositoryChangeSource _source = Substitute.For<IRepositoryChangeSource>();
    private readonly ChangeTools _tools;

    public ChangeToolsTests()
    {
        _tools = new ChangeTools(_source, Shop, Started, NullLogger.Instance);

        _source
            .ListChangesAsync(Shop, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Timeout]);
    }

    [Fact]
    public async Task ListingReadsOnlyTheMappedRepositoryInTheWindowBeforeTheProblem()
    {
        var text = await _tools.ListRecentChangesAsync();

        await _source.Received(1).ListChangesAsync(
            Shop,
            Started - ChangeTools.Lookback,
            Started + ChangeTools.Grace,
            ChangeTools.MaxChanges,
            Arg.Any<CancellationToken>()
        );

        Assert.Contains("a1b2c3d", text);
        Assert.Contains("\"minutesBeforeProblem\":15", text);
    }

    [Fact]
    public async Task OnlyAListedCommitCanBeInspected()
    {
        var refused = await _tools.InspectChangeAsync("a1b2c3d");

        Assert.Contains("list_recent_changes", refused);
        await _source.DidNotReceive().GetChangeAsync(Arg.Any<RepositoryMapping>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _tools.ListRecentChangesAsync();
        _source
            .GetChangeAsync(Shop, Timeout.Sha, Arg.Any<CancellationToken>())
            .Returns(new ChangeDetail(Timeout, "Reduce payment gateway timeout to 500ms", [new ChangedFile("src/PaymentClient.cs", "modified", 1, 1, "-30s\n+500ms")]));

        // The short sha the list gave out is enough; the full one is what goes to GitHub.
        var detail = await _tools.InspectChangeAsync("a1b2c3d");

        Assert.Contains("PaymentClient.cs", detail);
    }

    [Fact]
    public async Task RelatedChangesAreOnlyCommitsThisAnalysisSaw()
    {
        await _tools.ListRecentChangesAsync();

        var related = _tools.Resolve(["a1b2c3d", "A1B2C3D4E5F60718293A4B5C6D7E8F9012345678", "deadbee", "https://evil.example/x"]);

        var change = Assert.Single(related);
        Assert.Equal(Timeout.Sha, change.Sha);
        Assert.Equal("Reduce payment gateway timeout to 500ms", change.Title);
        Assert.Equal("https://github.com/acme/shop/commit/a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", change.Url);
    }

    [Fact]
    public void NothingSeenMeansNothingRelated()
    {
        Assert.Empty(_tools.Resolve(["a1b2c3d"]));
        Assert.Empty(_tools.Resolve(null));
    }

    [Fact]
    public async Task AnUnreachableGitHubIsTextForTheModelNotAFailedAnalysis()
    {
        _source
            .ListChangesAsync(Arg.Any<RepositoryMapping>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("503"));

        var text = await _tools.ListRecentChangesAsync();

        Assert.Contains("unavailable", text);
    }

    [Fact]
    public async Task TheModelCannotLoopOnTheTools()
    {
        for (var i = 0; i < ChangeTools.MaxListCalls + 3; i++)
            await _tools.ListRecentChangesAsync();

        await _source.Received(ChangeTools.MaxListCalls).ListChangesAsync(
            Arg.Any<RepositoryMapping>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        );

        _source.GetChangeAsync(Shop, Timeout.Sha, Arg.Any<CancellationToken>()).Returns((ChangeDetail?)null);

        for (var i = 0; i < ChangeTools.MaxInspections + 3; i++)
            await _tools.InspectChangeAsync("a1b2c3d");

        await _source.Received(ChangeTools.MaxInspections).GetChangeAsync(Shop, Timeout.Sha, Arg.Any<CancellationToken>());
    }
}
