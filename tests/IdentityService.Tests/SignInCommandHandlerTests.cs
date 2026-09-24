using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.SignIn;
using IdentityService.Application.Sessions;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IdentityService.Tests;

// The three ways a sign-in fails have to be one answer from outside and three lines in the log.
public sealed class SignInCommandHandlerTests
{
    private const string DummyHash = "$2a$12$dummy";
    private const string RealHash = "$2a$12$real";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenRepository _refreshTokens =
        Substitute.For<IRefreshTokenRepository>();
    private readonly IOrganizationRepository _organizations =
        Substitute.For<IOrganizationRepository>();
    private readonly ITokenGenerator _tokens = Substitute.For<ITokenGenerator>();

    private readonly Organization _organization = Organization.Create("Acme Operations");

    public SignInCommandHandlerTests()
    {
        _hasher.DummyHash.Returns(DummyHash);

        _tokens
            .CreateAccessToken(Arg.Any<User>())
            .Returns(new AccessToken("access", DateTime.UtcNow.AddMinutes(15)));

        _tokens
            .CreateRefreshToken()
            .Returns(new RefreshTokenPair("raw", "hash", DateTime.UtcNow.AddDays(14)));

        _organizations
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_organization);
    }

    private User GivenUser(bool active = true) =>
        User.Create(_organization.Id, "ops@example.com", "Ops", RealHash, UserRole.Engineer, active);

    private SignInCommandHandler Handler() =>
        new(
            _users,
            _hasher,
            new SessionIssuer(_tokens, _refreshTokens, _organizations),
            _refreshTokens,
            NullLogger<SignInCommandHandler>.Instance
        );

    private Task<IdentityService.Application.DTOs.IssuedSession?> When(
        string password = "right"
    ) => Handler().Handle(new SignInCommand("ops@example.com", password), CancellationToken.None);

    [Fact]
    public async Task ACorrectPasswordIssuesASession()
    {
        var user = GivenUser();
        _users.GetByEmailAsync("ops@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("right", RealHash).Returns(true);

        var session = await When();

        Assert.NotNull(session);
        Assert.Equal("access", session.Access.Value);
        Assert.Equal("raw", session.Refresh.Value);
        Assert.Equal(user.Id, session.User.Id);
        Assert.Equal("Engineer", session.User.Role);
        Assert.Equal("Acme Operations", session.User.OrganizationName);
    }

    [Fact]
    public async Task ACorrectPasswordStoresTheRefreshTokenAsAHash()
    {
        _users.GetByEmailAsync("ops@example.com", Arg.Any<CancellationToken>()).Returns(GivenUser());
        _hasher.Verify("right", RealHash).Returns(true);

        await When();

        await _refreshTokens
            .Received(1)
            .AddAsync(Arg.Is<RefreshToken>(t => t.TokenHash == "hash"), Arg.Any<CancellationToken>());

        await _refreshTokens.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AWrongPasswordIsRejected()
    {
        _users.GetByEmailAsync("ops@example.com", Arg.Any<CancellationToken>()).Returns(GivenUser());
        _hasher.Verify(Arg.Any<string>(), RealHash).Returns(false);

        Assert.Null(await When("wrong"));

        await _refreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnUnknownAddressIsRejected()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        Assert.Null(await When());
    }

    // The point of the dummy hash. Returning early for an unknown address would make sign-in fast
    // when there is no account and slow when there is, which answers "does this person have an
    // account here" to anyone willing to time it.
    [Fact]
    public async Task AnUnknownAddressStillSpendsTheTimeAVerificationWouldCost()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await When();

        _hasher.Received(1).Verify("right", DummyHash);
    }

    // Same answer as a wrong password, deliberately: the distinction belongs in the log, not in
    // the response.
    [Fact]
    public async Task ADeactivatedAccountIsRejectedEvenWithTheRightPassword()
    {
        _users
            .GetByEmailAsync("ops@example.com", Arg.Any<CancellationToken>())
            .Returns(GivenUser(active: false));
        _hasher.Verify("right", RealHash).Returns(true);

        Assert.Null(await When());

        await _refreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }
}
