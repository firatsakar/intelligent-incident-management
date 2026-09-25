using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Changes;
using AgentOrchestrator.Application.Commands.TestGitHubConnection;
using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AgentOrchestrator.Tests;

// The Test button: one read per mapped repository, a verdict per repository, GitHub's own reason
// when it failed — and a failure to reach GitHub at all is an answer, not a 500.
public sealed class TestGitHubConnectionTests
{
    private static readonly RepositoryMapping Shop = new("checkout-service", "acme", "shop", null);
    private static readonly RepositoryMapping Missing = new("search-service", "acme", "gone", null);

    private readonly IGitHubConnectionRepository _connections = Substitute.For<IGitHubConnectionRepository>();
    private readonly IRepositoryChangeSourceFactory _factory = Substitute.For<IRepositoryChangeSourceFactory>();
    private readonly IRepositoryChangeSource _source = Substitute.For<IRepositoryChangeSource>();
    private readonly TestGitHubConnectionCommandHandler _handler;

    public TestGitHubConnectionTests()
    {
        _handler = new TestGitHubConnectionCommandHandler(
            _connections,
            _factory,
            NullLogger<TestGitHubConnectionCommandHandler>.Instance
        );
    }

    private void GivenConnection(params RepositoryMapping[] repositories) =>
        _connections
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(GitHubConnection.Create(Guid.NewGuid(), "ghp_secret", repositories, true));

    [Fact]
    public async Task EachRepositoryGetsItsOwnVerdict()
    {
        GivenConnection(Shop, Missing);
        _factory.OpenAsync("ghp_secret", Arg.Any<CancellationToken>()).Returns(_source);
        _source
            .ListChangesAsync(Shop, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new RecentChange("abc1234", "Fix", "ayse", DateTime.UtcNow)]);
        _source
            .ListChangesAsync(Missing, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Not Found"));

        var result = await _handler.Handle(new TestGitHubConnectionCommand(), CancellationToken.None);

        var shop = result.Repositories.Single(r => r.Service == "checkout-service");
        Assert.True(shop.Ok);
        Assert.Equal(1, shop.RecentChanges);
        Assert.Equal("acme/shop", shop.Repository);

        var missing = result.Repositories.Single(r => r.Service == "search-service");
        Assert.False(missing.Ok);
        Assert.Equal("Not Found", missing.Error);

        await _source.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task UnreachableGitHubIsReportedOnEveryRow()
    {
        GivenConnection(Shop, Missing);
        _factory.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("401 Unauthorized"));

        var result = await _handler.Handle(new TestGitHubConnectionCommand(), CancellationToken.None);

        Assert.All(result.Repositories, row => Assert.False(row.Ok));
        Assert.All(result.Repositories, row => Assert.Equal("401 Unauthorized", row.Error));
    }

    [Fact]
    public async Task NothingToTestIsAValidationFailure()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(new TestGitHubConnectionCommand(), CancellationToken.None));

        GivenConnection();

        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(new TestGitHubConnectionCommand(), CancellationToken.None));
    }
}
