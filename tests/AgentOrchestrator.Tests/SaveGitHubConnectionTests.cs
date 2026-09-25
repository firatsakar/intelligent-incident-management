using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Commands.SaveGitHubConnection;
using AgentOrchestrator.Application.DTOs;
using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.ValueObjects;
using BuildingBlocks.SharedKernel;
using FluentValidation;
using NSubstitute;

namespace AgentOrchestrator.Tests;

// The form's rules, and the one the form cannot check itself: a first save has to bring a token,
// and no answer ever carries one back.
public sealed class SaveGitHubConnectionTests
{
    private static readonly Guid Organization = Guid.NewGuid();

    private readonly IGitHubConnectionRepository _connections = Substitute.For<IGitHubConnectionRepository>();
    private readonly SaveGitHubConnectionCommandHandler _handler;
    private readonly SaveGitHubConnectionCommandValidator _validator = new();

    private static readonly RepositoryMappingDto Checkout = new("checkout-service", "acme", "shop", " main ");

    public SaveGitHubConnectionTests()
    {
        var organization = new OrganizationContext();
        organization.Set(Organization);

        _handler = new SaveGitHubConnectionCommandHandler(_connections, organization);
    }

    [Fact]
    public async Task AFirstSaveCreatesTheConnectionAndTheAnswerHasNoToken()
    {
        GitHubConnection? added = null;
        await _connections.AddAsync(Arg.Do<GitHubConnection>(c => added = c), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(
            new SaveGitHubConnectionCommand("ghp_secret", true, [Checkout]),
            CancellationToken.None
        );

        Assert.NotNull(added);
        Assert.Equal(Organization, added.OrganizationId);
        Assert.Equal(new RepositoryMapping("checkout-service", "acme", "shop", "main"), Assert.Single(added.Repositories));

        Assert.True(result.IsConfigured);
        Assert.True(result.HasToken);
        Assert.DoesNotContain(
            typeof(GitHubConnectionDto).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.Ordinal) && property.Name != nameof(GitHubConnectionDto.HasToken)
        );
    }

    [Fact]
    public async Task AFirstSaveWithoutATokenIsRefusedOnThatField()
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(new SaveGitHubConnectionCommand(null, true, [Checkout]), CancellationToken.None)
        );

        Assert.Equal("Token", Assert.Single(refused.Errors).PropertyName);
    }

    [Fact]
    public async Task ALaterSaveWithoutATokenKeepsTheStoredOne()
    {
        var existing = GitHubConnection.Create(Organization, "ghp_secret", [], true);
        _connections.GetAsync(Arg.Any<CancellationToken>()).Returns(existing);

        await _handler.Handle(new SaveGitHubConnectionCommand("", false, [Checkout]), CancellationToken.None);

        Assert.Equal("ghp_secret", existing.Token);
        Assert.False(existing.IsEnabled);
        Assert.Single(existing.Repositories);
        await _connections.DidNotReceive().AddAsync(Arg.Any<GitHubConnection>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("acme", "shop", null, true)]
    [InlineData("acme-inc", "shop.api_v2", "release/2026-09", true)]
    [InlineData("-acme", "shop", null, false)]
    [InlineData("acme", "shop/extra", null, false)]
    [InlineData("acme", "shop", "has space", false)]
    [InlineData("", "shop", null, false)]
    public void RepositoryNamesFollowGitHubsRules(string owner, string repository, string? branch, bool valid)
    {
        var result = _validator.Validate(
            new SaveGitHubConnectionCommand("ghp", true, [new RepositoryMappingDto("svc", owner, repository, branch)])
        );

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void AServiceCanBeMappedOnlyOnce()
    {
        var result = _validator.Validate(
            new SaveGitHubConnectionCommand(
                "ghp",
                true,
                [
                    new RepositoryMappingDto("checkout-service", "acme", "shop", null),
                    new RepositoryMappingDto("Checkout-Service", "acme", "other", null),
                ]
            )
        );

        Assert.False(result.IsValid);
    }
}
