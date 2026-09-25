using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.RefreshSession;
using IdentityService.Application.Sessions;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IdentityService.Tests;

// Rotation, and the part that matters more: what happens when a token turns up twice.
public sealed class RefreshSessionCommandHandlerTests
{
    private const string Presented = "presented-raw";
    private const string PresentedHash = "presented-hash";
    private const string NextHash = "next-hash";

    private readonly IRefreshTokenRepository _refreshTokens =
        Substitute.For<IRefreshTokenRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IOrganizationRepository _organizations =
        Substitute.For<IOrganizationRepository>();
    private readonly ITokenGenerator _tokens = Substitute.For<ITokenGenerator>();

    private readonly Organization _organization = Organization.Create("Acme Operations");
    private readonly User _user;

    public RefreshSessionCommandHandlerTests()
    {
        _user = User.Create(_organization.Id, "ops@example.com", "Ops", "hash", UserRole.Engineer);

        _tokens.HashRefreshToken(Presented).Returns(PresentedHash);
        _tokens
            .CreateAccessToken(Arg.Any<User>())
            .Returns(new AccessToken("access", DateTime.UtcNow.AddMinutes(15)));
        _tokens
            .CreateRefreshToken()
            .Returns(new RefreshTokenPair("next-raw", NextHash, DateTime.UtcNow.AddDays(14)));

        _organizations
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_organization);
        _users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
    }

    private RefreshToken GivenStoredToken(TimeSpan? life = null) =>
        RefreshToken.Issue(_user.Id, PresentedHash, DateTime.UtcNow + (life ?? TimeSpan.FromDays(14)));

    private void Stored(RefreshToken? token) =>
        _refreshTokens.GetByHashAsync(PresentedHash, Arg.Any<CancellationToken>()).Returns(token);

    private Task<IdentityService.Application.DTOs.IssuedSession?> When() =>
        new RefreshSessionCommandHandler(
            _refreshTokens,
            _users,
            _tokens,
            new SessionIssuer(_tokens, _refreshTokens, _organizations),
            NullLogger<RefreshSessionCommandHandler>.Instance
        ).Handle(new RefreshSessionCommand(Presented), CancellationToken.None);

    [Fact]
    public async Task AnActiveTokenIsExchangedForANewPair()
    {
        Stored(GivenStoredToken());

        var session = await When();

        Assert.NotNull(session);
        Assert.Equal("next-raw", session.Refresh.Value);
        Assert.Equal(_user.Id, session.User.Id);
    }

    [Fact]
    public async Task TheOldTokenIsRetiredAndNamesItsSuccessor()
    {
        var stored = GivenStoredToken();
        Stored(stored);

        await When();

        Assert.NotNull(stored.RevokedAt);
        Assert.Equal(NextHash, stored.ReplacedByHash);
        Assert.False(stored.IsActive(DateTime.UtcNow));
    }

    [Fact]
    public async Task ATokenThatWasNeverIssuedBuysNothing()
    {
        Stored(null);

        Assert.Null(await When());

        await _refreshTokens.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // Simply too old. A client that slept through its own expiry signs in again; there is nothing
    // here to defend against, so nothing else is revoked.
    [Fact]
    public async Task AnExpiredTokenIsRejectedWithoutEndingAnythingElse()
    {
        Stored(GivenStoredToken(TimeSpan.FromMinutes(-1)));

        Assert.Null(await When());

        await _refreshTokens
            .DidNotReceive()
            .GetActiveByUserAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    // The case the whole design exists for. A token that has already been exchanged is either a
    // stolen copy or a stale client, and from here they are identical — so every token that
    // account still holds goes.
    [Fact]
    public async Task ASpentTokenRevokesEveryOtherTokenTheUserHolds()
    {
        var spent = GivenStoredToken();
        spent.RotateTo("some-earlier-successor", DateTime.UtcNow.AddMinutes(-5));
        Stored(spent);

        var alsoActive = RefreshToken.Issue(_user.Id, "elsewhere", DateTime.UtcNow.AddDays(14));

        _refreshTokens
            .GetActiveByUserAsync(_user.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([alsoActive]);

        Assert.Null(await When());

        Assert.False(alsoActive.IsActive(DateTime.UtcNow));
        await _refreshTokens.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // Signing out revokes without rotating, so the token has no successor. A client that fires a
    // refresh while the sign-out is still in flight — or a person who hits back — is presenting a
    // revoked token in perfectly ordinary circumstances, and treating that as theft would mean
    // signing out of a laptop logs the phone out too. This was found by walking the endpoints,
    // not by reading the code: the first version keyed reuse detection on RevokedAt.
    [Fact]
    public async Task ATokenRevokedBySigningOutIsRefusedWithoutEndingOtherSessions()
    {
        var signedOut = GivenStoredToken();
        signedOut.Revoke(DateTime.UtcNow.AddMinutes(-1));
        Stored(signedOut);

        Assert.Null(await When());

        await _refreshTokens
            .DidNotReceive()
            .GetActiveByUserAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    // A valid token for an account switched off mid-session. The token cannot be withdrawn, which
    // is exactly why it has to be ended here rather than left to expire on its own.
    [Fact]
    public async Task ADeactivatedAccountEndsTheTokenRatherThanRenewingIt()
    {
        var stored = GivenStoredToken();
        Stored(stored);

        _user.Deactivate();

        Assert.Null(await When());

        Assert.NotNull(stored.RevokedAt);
        Assert.Null(stored.ReplacedByHash);
    }

    [Fact]
    public async Task AVanishedAccountEndsTheTokenToo()
    {
        var stored = GivenStoredToken();
        Stored(stored);

        _users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns((User?)null);

        Assert.Null(await When());

        Assert.NotNull(stored.RevokedAt);
    }
}
