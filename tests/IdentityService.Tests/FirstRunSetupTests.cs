using BuildingBlocks.SharedKernel;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.CompleteSetup;
using IdentityService.Application.DTOs;
using IdentityService.Application.Sessions;
using IdentityService.Application.Setup;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using IdentityService.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IdentityService.Tests;

// The first-run setup (Adım 25): an empty installation is claimed once, by whoever can read the
// server's log, and never again.
public sealed class FirstRunSetupTests
{
    public sealed class Code
    {
        [Fact]
        public void IsTwelveUnambiguousCharactersInThreeGroups()
        {
            var code = new SetupCode().Issue();

            Assert.Matches("^[A-HJ-KM-NP-TW-Z2-9]{4}-[A-HJ-KM-NP-TW-Z2-9]{4}-[A-HJ-KM-NP-TW-Z2-9]{4}$", code);
        }

        [Fact]
        public void ForgivesCaseDashesAndSpacesButNothingElse()
        {
            var setup = new SetupCode();
            var code = setup.Issue();

            Assert.True(setup.Matches(code));
            Assert.True(setup.Matches($"  {code.Replace("-", " ").ToLowerInvariant()} "));
            Assert.False(setup.Matches(code[..^1] + (code[^1] == 'A' ? 'B' : 'A')));
            Assert.False(setup.Matches(""));
            Assert.False(setup.Matches(null));
        }

        [Fact]
        public void NothingMatchesBeforeItIsIssuedOrAfterItIsSpent()
        {
            var setup = new SetupCode();

            Assert.False(setup.IsIssued);
            Assert.False(setup.Matches("ABCD-EFGH-JKMN"));

            var code = setup.Issue();
            setup.Consume();

            Assert.False(setup.IsIssued);
            Assert.False(setup.Matches(code));
        }

        [Fact]
        public void ANewCodeReplacesTheOldOne()
        {
            var setup = new SetupCode();
            var first = setup.Issue();
            var second = setup.Issue();

            Assert.NotEqual(first, second);
            Assert.False(setup.Matches(first));
            Assert.True(setup.Matches(second));
        }
    }

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenGenerator _tokens = Substitute.For<ITokenGenerator>();
    private readonly SetupCode _setup = new();
    private readonly string _code;

    private readonly List<Organization> _addedOrganizations = [];
    private readonly List<User> _addedUsers = [];

    public FirstRunSetupTests()
    {
        _code = _setup.Issue();

        _organizations
            .When(x => x.AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>()))
            .Do(call => _addedOrganizations.Add(call.Arg<Organization>()));
        _users
            .When(x => x.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()))
            .Do(call => _addedUsers.Add(call.Arg<User>()));

        // The database is empty until something is added, as the real one is.
        _users.AnyAsync(Arg.Any<CancellationToken>()).Returns(_ => _addedUsers.Count > 0);

        _hasher.Hash(Arg.Any<string>()).Returns(call => $"hashed:{call.Arg<string>()}");
        _tokens.CreateAccessToken(Arg.Any<User>()).Returns(new AccessToken("access", DateTime.UtcNow.AddMinutes(15)));
        _tokens.CreateRefreshToken().Returns(new RefreshTokenPair("refresh", "refresh-hash", DateTime.UtcNow.AddDays(14)));
    }

    private CompleteSetupCommandHandler Handler() =>
        new(
            _users,
            _organizations,
            _hasher,
            new SessionIssuer(_tokens, _refreshTokens, _organizations),
            _setup,
            new OrganizationContext(),
            NullLogger<CompleteSetupCommandHandler>.Instance
        );

    private Task<IssuedSession> Complete(string code, string email = "ayse@acme.test") =>
        Handler()
            .Handle(new CompleteSetupCommand(code, "  Acme Platform  ", "Ayşe Yılmaz", email, "correct horse battery"), CancellationToken.None);

    [Fact]
    public async Task TheRightCodeCreatesTheOrganisationAndItsFirstAdminAndSpendsTheCode()
    {
        await Complete(_code);

        var organization = Assert.Single(_addedOrganizations);
        Assert.Equal("Acme Platform", organization.Name);

        var admin = Assert.Single(_addedUsers);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(organization.Id, admin.OrganizationId);
        Assert.Equal("hashed:correct horse battery", admin.PasswordHash);

        Assert.False(_setup.IsIssued);
        await _refreshTokens.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AWrongCodeIsRefusedAndChangesNothing()
    {
        await Assert.ThrowsAsync<SetupNotAvailableException>(() => Complete("WRNG-WRNG-WRNG"));

        Assert.Empty(_addedOrganizations);
        Assert.Empty(_addedUsers);
        Assert.True(_setup.IsIssued);
    }

    [Fact]
    public async Task AnInstallationWithUsersIsNeverSetUpAgainEvenWithTheCode()
    {
        _users.AnyAsync(Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<SetupNotAvailableException>(() => Complete(_code));

        Assert.Empty(_addedOrganizations);
    }

    [Fact]
    public async Task OfTwoRequestsWithTheRightCodeExactlyOneWins()
    {
        var results = await Task.WhenAll(Attempt("first@acme.test"), Attempt("second@acme.test"));

        Assert.Single(results, won => won);
        Assert.Single(_addedOrganizations);
        Assert.Single(_addedUsers);

        async Task<bool> Attempt(string email)
        {
            try
            {
                await Complete(_code, email);
                return true;
            }
            catch (SetupNotAvailableException)
            {
                return false;
            }
        }
    }
}
