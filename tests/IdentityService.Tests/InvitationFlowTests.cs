using BuildingBlocks.SharedKernel;
using FluentValidation;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.AcceptInvitation;
using IdentityService.Application.Commands.ChangePassword;
using IdentityService.Application.Commands.CompletePasswordReset;
using IdentityService.Application.Commands.InviteMember;
using IdentityService.Application.Commands.IssuePasswordReset;
using IdentityService.Application.Sessions;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IdentityService.Tests;

// The ways into an account that are not signing in: an invitation, a reset link, a change of one's
// own password. What they share is that the organisation and the role come from a row the Admin
// wrote, never from the request, and that every link works once.
public sealed class InvitationFlowTests
{
    private readonly Guid _organization = Guid.NewGuid();
    private readonly OrganizationContext _scope = new();

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IInvitationRepository _invitations = Substitute.For<IInvitationRepository>();
    private readonly IPasswordResetRepository _resets = Substitute.For<IPasswordResetRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IConsoleLinks _links = Substitute.For<IConsoleLinks>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenGenerator _tokens = Substitute.For<ITokenGenerator>();

    private readonly List<Invitation> _addedInvitations = [];
    private readonly List<User> _addedUsers = [];
    private readonly Guid _actor = Guid.NewGuid();

    public InvitationFlowTests()
    {
        _scope.Set(_organization);

        _links.Invitation(Arg.Any<string>()).Returns(call => $"http://console/invite/{call.Arg<string>()}");
        _links.PasswordReset(Arg.Any<string>()).Returns(call => $"http://console/reset/{call.Arg<string>()}");

        _invitations.ListPendingForEmailAsync(default, default!, default, default).ReturnsForAnyArgs([]);
        _invitations
            .When(x => x.AddAsync(Arg.Any<Invitation>(), Arg.Any<CancellationToken>()))
            .Do(call => _addedInvitations.Add(call.Arg<Invitation>()));

        _users
            .When(x => x.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()))
            .Do(call => _addedUsers.Add(call.Arg<User>()));

        _refreshTokens.GetActiveByUserAsync(default, default, default).ReturnsForAnyArgs([]);
        _resets.ListUsableForUserAsync(default, default, default).ReturnsForAnyArgs([]);

        _hasher.Hash(Arg.Any<string>()).Returns(call => $"hashed:{call.Arg<string>()}");
        _tokens.CreateAccessToken(Arg.Any<User>()).Returns(new AccessToken("access", DateTime.UtcNow.AddMinutes(15)));
        _tokens.CreateRefreshToken().Returns(new RefreshTokenPair("refresh", "refresh-hash", DateTime.UtcNow.AddDays(14)));
    }

    private SessionIssuer Issuer() => new(_tokens, _refreshTokens, _organizations);

    private InviteMemberCommandHandler Inviter() =>
        new(_users, _invitations, _organizations, _email, _links, _scope, NullLogger<InviteMemberCommandHandler>.Instance);

    private AcceptInvitationCommandHandler Acceptor() =>
        new(_invitations, _users, _hasher, Issuer(), NullLogger<AcceptInvitationCommandHandler>.Instance);

    private (Invitation Invitation, string Token) GivenPendingInvitation(UserRole role = UserRole.Engineer, DateTime? issuedAt = null)
    {
        var token = OneTimeToken.Generate();
        var invitation = Invitation.Issue(_organization, "new.person@canary.test", role, token.Hash, _actor, issuedAt ?? DateTime.UtcNow);
        _invitations.FindByTokenHashForAcceptAsync(token.Hash, Arg.Any<CancellationToken>()).Returns(invitation);

        return (invitation, token.Token);
    }

    // ---- inviting -----------------------------------------------------------------------------

    [Fact]
    public async Task AnInvitationIsForTheAdminsOrganisationAndItsLinkIsTheOnlyCopyOfTheToken()
    {
        var result = await Inviter().Handle(new InviteMemberCommand(_actor, "New.Person@Canary.test", UserRole.Viewer), default);

        var stored = Assert.Single(_addedInvitations);
        Assert.Equal(_organization, stored.OrganizationId);
        Assert.Equal(UserRole.Viewer, stored.Role);

        var token = result.Issued.Link.Split('/').Last();
        Assert.Equal(OneTimeToken.Hash(token), stored.TokenHash);
        Assert.True(result.Issued.EmailSent);
        await _email.Received(1).SendAsync(Arg.Is<OutgoingEmail>(mail => mail.To == "new.person@canary.test" && mail.Text.Contains(result.Issued.Link)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMailOutageDoesNotLoseTheInvitation()
    {
        _email.SendAsync(Arg.Any<OutgoingEmail>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("SMTP down"));

        var result = await Inviter().Handle(new InviteMemberCommand(_actor, "a@canary.test", UserRole.Viewer), default);

        Assert.False(result.Issued.EmailSent);
        Assert.Single(_addedInvitations);
        Assert.StartsWith("http://console/invite/", result.Issued.Link);
    }

    [Fact]
    public async Task AMemberCannotBeInvitedAgain()
    {
        _users.GetByEmailAsync("a@canary.test", Arg.Any<CancellationToken>())
            .Returns(User.Create(_organization, "a@canary.test", "A", "h", UserRole.Viewer));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Inviter().Handle(new InviteMemberCommand(_actor, "a@canary.test", UserRole.Viewer), default));

        Assert.Contains("already a member", error.Message);
    }

    [Fact]
    public async Task AnAddressWithAnAccountElsewhereIsRefusedWithoutSayingWhere()
    {
        _users.GetByEmailAsync("b@elsewhere.test", Arg.Any<CancellationToken>())
            .Returns(User.Create(Guid.NewGuid(), "b@elsewhere.test", "B", "h", UserRole.Admin));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Inviter().Handle(new InviteMemberCommand(_actor, "b@elsewhere.test", UserRole.Viewer), default));

        Assert.DoesNotContain("member", error.Message);
        Assert.Empty(_addedInvitations);
    }

    [Fact]
    public async Task ANewInvitationReplacesAPendingOneToTheSameAddress()
    {
        var (older, _) = GivenPendingInvitation();
        _invitations.ListPendingForEmailAsync(_organization, "new.person@canary.test", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([older]);

        await Inviter().Handle(new InviteMemberCommand(_actor, "new.person@canary.test", UserRole.Admin), default);

        Assert.NotNull(older.RevokedAt);
    }

    [Fact]
    public void AnInvitationNeedsARealAddress()
    {
        var result = new InviteMemberCommandValidator().Validate(new InviteMemberCommand(_actor, "not-an-address", UserRole.Viewer));

        Assert.False(result.IsValid);
    }

    // ---- accepting ----------------------------------------------------------------------------

    [Fact]
    public async Task AcceptingCreatesTheAccountTheInvitationDescribesAndSignsItIn()
    {
        var (invitation, token) = GivenPendingInvitation(UserRole.Engineer);

        var session = await Acceptor().Handle(new AcceptInvitationCommand(token, "New Person", "a long enough password"), default);

        var user = Assert.Single(_addedUsers);
        Assert.Equal(_organization, user.OrganizationId);
        Assert.Equal(UserRole.Engineer, user.Role);
        Assert.Equal("new.person@canary.test", user.Email);
        Assert.Equal("hashed:a long enough password", user.PasswordHash);
        Assert.Equal(user.Id, invitation.AcceptedUserId);
        Assert.Equal(user.Id, session.User.Id);
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnExpiredInvitationIsNotAWayIn()
    {
        var (_, token) = GivenPendingInvitation(issuedAt: DateTime.UtcNow - Invitation.Validity - TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<LinkNotFoundException>(() =>
            Acceptor().Handle(new AcceptInvitationCommand(token, "Late", "a long enough password"), default));

        Assert.Empty(_addedUsers);
    }

    [Fact]
    public async Task AnUnknownTokenGetsTheSameAnswerAsAnExpiredOne()
    {
        await Assert.ThrowsAsync<LinkNotFoundException>(() =>
            Acceptor().Handle(new AcceptInvitationCommand("guessed", "Guess", "a long enough password"), default));
    }

    [Fact]
    public async Task AnAddressThatFoundAnAccountMeanwhileCannotAcceptAgain()
    {
        var (_, token) = GivenPendingInvitation();
        _users.GetByEmailAsync("new.person@canary.test", Arg.Any<CancellationToken>())
            .Returns(User.Create(Guid.NewGuid(), "new.person@canary.test", "Other", "h", UserRole.Viewer));

        await Assert.ThrowsAsync<LinkNotFoundException>(() =>
            Acceptor().Handle(new AcceptInvitationCommand(token, "Twice", "a long enough password"), default));
    }

    [Theory]
    [InlineData("short", false)]
    [InlineData("exactly twelve", true)]
    [InlineData("ÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇÇ", false)]
    public void ANewPasswordIsLongEnoughAndNotLongerThanTheHashReads(string password, bool valid)
    {
        // The last one is 37 characters and 74 bytes: past what BCrypt reads.
        var result = new AcceptInvitationCommandValidator().Validate(new AcceptInvitationCommand("t", "Name", password));

        Assert.Equal(valid, result.IsValid);
    }

    // ---- resets -------------------------------------------------------------------------------

    private IssuePasswordResetCommandHandler ResetIssuer() =>
        new(_users, _resets, _organizations, _email, _links, _scope, NullLogger<IssuePasswordResetCommandHandler>.Instance);

    private User GivenMember(bool active = true)
    {
        var user = User.Create(_organization, "member@canary.test", "Member", "old", UserRole.Engineer, active);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        return user;
    }

    [Fact]
    public async Task AnAdminIssuesAResetLinkAndOnlyTheNewestWorks()
    {
        var member = GivenMember();
        var older = PasswordReset.Issue(_organization, member.Id, "older", _actor, DateTime.UtcNow);
        _resets.ListUsableForUserAsync(member.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([older]);

        var issued = await ResetIssuer().Handle(new IssuePasswordResetCommand(_actor, member.Id), default);

        Assert.StartsWith("http://console/reset/", issued.Link);
        Assert.NotNull(older.RevokedAt);
    }

    [Fact]
    public async Task NobodyIssuesAResetLinkForThemselves()
    {
        var member = GivenMember();

        await Assert.ThrowsAsync<ValidationException>(() =>
            ResetIssuer().Handle(new IssuePasswordResetCommand(member.Id, member.Id), default));
    }

    [Fact]
    public async Task ADeactivatedAccountGetsNoResetLink()
    {
        var member = GivenMember(active: false);

        await Assert.ThrowsAsync<ValidationException>(() =>
            ResetIssuer().Handle(new IssuePasswordResetCommand(_actor, member.Id), default));
    }

    [Fact]
    public async Task AnotherOrganisationsMemberGetsNoResetLink()
    {
        var stranger = User.Create(Guid.NewGuid(), "x@elsewhere.test", "X", "h", UserRole.Viewer);
        _users.GetByIdAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);

        await Assert.ThrowsAsync<MemberNotFoundException>(() =>
            ResetIssuer().Handle(new IssuePasswordResetCommand(_actor, stranger.Id), default));
    }

    [Fact]
    public async Task CompletingAResetChangesThePasswordAndEndsEveryOtherSession()
    {
        var member = GivenMember();
        var token = OneTimeToken.Generate();
        var reset = PasswordReset.Issue(_organization, member.Id, token.Hash, _actor, DateTime.UtcNow);
        _resets.FindByTokenHashForResetAsync(token.Hash, Arg.Any<CancellationToken>()).Returns(reset);
        var session = RefreshToken.Issue(member.Id, "still-open", DateTime.UtcNow.AddDays(5));
        _refreshTokens.GetActiveByUserAsync(member.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([session]);

        await new CompletePasswordResetCommandHandler(_resets, _users, _refreshTokens, _hasher, Issuer())
            .Handle(new CompletePasswordResetCommand(token.Token, "a brand new password"), default);

        Assert.Equal("hashed:a brand new password", member.PasswordHash);
        Assert.NotNull(reset.UsedAt);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task TheCurrentPasswordIsProofForChangingIt()
    {
        var member = GivenMember();
        _hasher.Verify("wrong", "old").Returns(false);

        await Assert.ThrowsAsync<ValidationException>(() =>
            new ChangePasswordCommandHandler(_users, _refreshTokens, _hasher, Issuer())
                .Handle(new ChangePasswordCommand(member.Id, "wrong", "a brand new password"), default));

        Assert.Equal("old", member.PasswordHash);
    }
}
