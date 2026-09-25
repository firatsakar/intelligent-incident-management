using System.Security.Claims;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BuildingBlocks.Tests;

// Adım 16.5, Fırat's rule: what belongs to the organisation itself — its integrations, telemetry
// sources, members — is seen and changed by its Admins only. These pin the two places that rule
// lives in shared code: the policy every service authorises against, and the hub group every
// configuration broadcast is addressed to.
public sealed class PlatformRolesTests
{
    private static ClaimsPrincipal Signed(string role) =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(PlatformClaims.Role, role),
                    new Claim(PlatformClaims.Organization, Guid.NewGuid().ToString()),
                ],
                authenticationType: "test",
                nameType: PlatformClaims.DisplayName,
                roleType: PlatformClaims.Role
            )
        );

    private static IAuthorizationService Authorization()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = new string('k', 48),
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformAuth(configuration);

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Engineer", false)]
    [InlineData("Viewer", false)]
    public async Task OnlyAnAdminAdministers(string role, bool allowed)
    {
        var result = await Authorization().AuthorizeAsync(Signed(role), PlatformPolicies.Administer);

        Assert.Equal(allowed, result.Succeeded);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Engineer", true)]
    [InlineData("Viewer", false)]
    public async Task AdminsAndEngineersWorkIncidents(string role, bool allowed)
    {
        var result = await Authorization().AuthorizeAsync(Signed(role), PlatformPolicies.Operate);

        Assert.Equal(allowed, result.Succeeded);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Engineer", false)]
    [InlineData("Viewer", false)]
    public async Task OnlyAnAdminsSocketJoinsTheAdminGroup(string role, bool joins)
    {
        // Configuration broadcasts go to this group alone. A socket that joined it without being an
        // Admin would hear the organisation's integrations and sources as they change.
        var user = Signed(role);
        var organization = Guid.Parse(user.FindFirst(PlatformClaims.Organization)!.Value);

        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(user);
        context.ConnectionId.Returns("connection");

        var hub = new TestHub { Context = context, Groups = groups };

        await hub.OnConnectedAsync();

        await groups.Received(1).AddToGroupAsync("connection", OrganizationGroups.For(organization), Arg.Any<CancellationToken>());
        await groups
            .Received(joins ? 1 : 0)
            .AddToGroupAsync("connection", OrganizationGroups.AdminsOf(organization), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheAdminGroupIsNotTheOrganisationsGroup()
    {
        var organization = Guid.NewGuid();

        Assert.NotEqual(OrganizationGroups.For(organization), OrganizationGroups.AdminsOf(organization));
    }

    private sealed class TestHub : OrganizationHub;
}
