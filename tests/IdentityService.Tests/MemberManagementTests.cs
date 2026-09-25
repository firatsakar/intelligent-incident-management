using BuildingBlocks.SharedKernel;
using FluentValidation;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.ChangeMemberRole;
using IdentityService.Application.Commands.SetMemberActive;
using IdentityService.Application.Queries.GetMembers;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using IdentityService.Domain.Exceptions;
using NSubstitute;

namespace IdentityService.Tests;

// An Admin manages the organisation's members. The rules worth pinning are the ones that keep an
// organisation from locking itself out, and the one that keeps it from seeing anybody else's.
public sealed class MemberManagementTests
{
    private readonly Guid _organization = Guid.NewGuid();
    private readonly OrganizationContext _scope = new();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();

    private readonly User _admin;

    public MemberManagementTests()
    {
        _scope.Set(_organization);
        _admin = Member(UserRole.Admin);
        _refreshTokens
            .GetActiveByUserAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private User Member(UserRole role, Guid? organization = null)
    {
        var user = User.Create(organization ?? _organization, $"{Guid.NewGuid():N}@canary.test", "Someone", "hash", role);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        return user;
    }

    private void GivenActiveAdmins(int count) =>
        _users.CountActiveAdminsAsync(_organization, Arg.Any<CancellationToken>()).Returns(count);

    private Task<Application.DTOs.MemberDto> ChangeRole(User member, UserRole role, User? actor = null) =>
        new ChangeMemberRoleCommandHandler(_users, _scope).Handle(
            new ChangeMemberRoleCommand((actor ?? _admin).Id, member.Id, role),
            CancellationToken.None
        );

    private Task<Application.DTOs.MemberDto> SetActive(User member, bool active, User? actor = null) =>
        new SetMemberActiveCommandHandler(_users, _refreshTokens, _scope).Handle(
            new SetMemberActiveCommand((actor ?? _admin).Id, member.Id, active),
            CancellationToken.None
        );

    [Fact]
    public async Task AnAdminPromotesAnEngineer()
    {
        var engineer = Member(UserRole.Engineer);

        var result = await ChangeRole(engineer, UserRole.Admin);

        Assert.Equal(UserRole.Admin, result.Role);
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnotherOrganisationsMemberIsNotFound()
    {
        // Not forbidden: whether an account exists in another organisation is not this Admin's to learn.
        var stranger = Member(UserRole.Viewer, organization: Guid.NewGuid());

        await Assert.ThrowsAsync<MemberNotFoundException>(() => ChangeRole(stranger, UserRole.Admin));
        await Assert.ThrowsAsync<MemberNotFoundException>(() => SetActive(stranger, false));
        await _users.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NobodyChangesTheirOwnRole()
    {
        GivenActiveAdmins(2);

        await Assert.ThrowsAsync<ValidationException>(() => ChangeRole(_admin, UserRole.Viewer, actor: _admin));
    }

    [Fact]
    public async Task NobodyDeactivatesThemselves()
    {
        GivenActiveAdmins(2);

        await Assert.ThrowsAsync<ValidationException>(() => SetActive(_admin, false, actor: _admin));
    }

    [Fact]
    public async Task TheLastActiveAdminCannotBeDemoted()
    {
        var onlyAdmin = Member(UserRole.Admin);
        var otherActor = Member(UserRole.Admin);
        GivenActiveAdmins(1);

        await Assert.ThrowsAsync<ValidationException>(() => ChangeRole(onlyAdmin, UserRole.Engineer, actor: otherActor));
        Assert.Equal(UserRole.Admin, onlyAdmin.Role);
    }

    [Fact]
    public async Task TheLastActiveAdminCannotBeDeactivated()
    {
        var onlyAdmin = Member(UserRole.Admin);
        var otherActor = Member(UserRole.Admin);
        GivenActiveAdmins(1);

        await Assert.ThrowsAsync<ValidationException>(() => SetActive(onlyAdmin, false, actor: otherActor));
        Assert.True(onlyAdmin.IsActive);
    }

    [Fact]
    public async Task WithASecondAdminOneCanBeDemoted()
    {
        var admin = Member(UserRole.Admin);
        GivenActiveAdmins(2);

        var result = await ChangeRole(admin, UserRole.Engineer);

        Assert.Equal(UserRole.Engineer, result.Role);
    }

    [Fact]
    public async Task DeactivatingEndsEverySessionTheMemberHolds()
    {
        var engineer = Member(UserRole.Engineer);
        var tokens = new[]
        {
            RefreshToken.Issue(engineer.Id, "a", DateTime.UtcNow.AddDays(10)),
            RefreshToken.Issue(engineer.Id, "b", DateTime.UtcNow.AddDays(10)),
        };
        _refreshTokens
            .GetActiveByUserAsync(engineer.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(tokens);

        var result = await SetActive(engineer, false);

        Assert.False(result.IsActive);
        Assert.All(tokens, token => Assert.NotNull(token.RevokedAt));
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ADeactivatedMemberCanBeLetBackIn()
    {
        var engineer = Member(UserRole.Engineer);
        engineer.Deactivate();

        var result = await SetActive(engineer, true);

        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task TheListIsTheCallersOrganisation()
    {
        _users.ListByOrganizationAsync(_organization, Arg.Any<CancellationToken>()).Returns([_admin]);

        var members = await new GetMembersQueryHandler(_users, _scope).Handle(new GetMembersQuery(), CancellationToken.None);

        Assert.Equal(_admin.Id, Assert.Single(members).Id);
        await _users.Received(1).ListByOrganizationAsync(_organization, Arg.Any<CancellationToken>());
    }
}
