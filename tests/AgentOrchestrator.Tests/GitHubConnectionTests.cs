using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Tests;

// Which repository an incident is read against decides whether the analysis looks at the right
// code or at none — never at a guess.
public sealed class GitHubConnectionTests
{
    private static readonly Guid Organization = Guid.NewGuid();

    private static readonly RepositoryMapping Checkout = new("checkout-service", "acme", "shop", "main");
    private static readonly RepositoryMapping Default = new(RepositoryMapping.AnyService, "acme", "platform", null);

    private static GitHubConnection Create(bool enabled = true, params RepositoryMapping[] repositories) =>
        GitHubConnection.Create(Organization, " ghp_secret ", repositories, enabled);

    [Fact]
    public void AServiceGetsItsOwnRepositoryWhateverTheCase()
    {
        var connection = Create(true, Checkout, Default);

        Assert.Equal(Checkout, connection.RepositoryFor("Checkout-Service"));
    }

    [Fact]
    public void AnUnmappedServiceFallsBackToTheDefault()
    {
        var connection = Create(true, Checkout, Default);

        Assert.Equal(Default, connection.RepositoryFor("search-service"));
        Assert.Equal(Default, connection.RepositoryFor(null));
    }

    [Fact]
    public void WithoutADefaultAnUnmappedServiceGetsNothing()
    {
        var connection = Create(true, Checkout);

        Assert.Null(connection.RepositoryFor("search-service"));
        Assert.Null(connection.RepositoryFor(null));
    }

    [Fact]
    public void ADisabledConnectionAnswersNothing()
    {
        var connection = Create(false, Checkout, Default);

        Assert.Null(connection.RepositoryFor("checkout-service"));
    }

    [Fact]
    public void TheTokenIsTrimmedAndABlankUpdateKeepsIt()
    {
        var connection = Create(true, Checkout);

        Assert.Equal("ghp_secret", connection.Token);

        connection.Update(token: "  ", [Default], isEnabled: false);

        Assert.Equal("ghp_secret", connection.Token);
        Assert.Equal(new[] { Default }, connection.Repositories);
        Assert.False(connection.IsEnabled);

        connection.Update(token: "ghp_rotated", [Default], isEnabled: true);

        Assert.Equal("ghp_rotated", connection.Token);
    }

    [Fact]
    public void ANewConnectionNeedsAToken()
    {
        Assert.Throws<ArgumentException>(() => GitHubConnection.Create(Organization, " ", [Checkout], true));
    }
}
