using BuildingBlocks.SharedKernel;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.RenameOrganization;
using IdentityService.Domain.Aggregates;
using NSubstitute;

namespace IdentityService.Tests;

// The organisation's name is corrected by its Admins (Adım 25). The request carries no id: the
// organisation renamed is always the caller's own.
public sealed class RenameOrganizationTests
{
    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly Organization _own = Organization.Create("Acme Operations");
    private readonly RenameOrganizationCommandHandler _handler;

    public RenameOrganizationTests()
    {
        var scope = new OrganizationContext();
        scope.Set(_own.Id);

        _organizations.GetByIdAsync(_own.Id, Arg.Any<CancellationToken>()).Returns(_own);
        _handler = new RenameOrganizationCommandHandler(_organizations, scope);
    }

    [Fact]
    public async Task RenamesTheCallersOwnOrganisationTrimmed()
    {
        var result = await _handler.Handle(new RenameOrganizationCommand("  Platform Team  "), CancellationToken.None);

        Assert.Equal("Platform Team", _own.Name);
        Assert.Equal(new OrganizationDto(_own.Id, "Platform Team"), result);
        await _organizations.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsRefused(string name)
    {
        Assert.False(new RenameOrganizationCommandValidator().Validate(new RenameOrganizationCommand(name)).IsValid);
    }

    [Fact]
    public void ANameLongerThanTheColumnIsRefused()
    {
        Assert.False(
            new RenameOrganizationCommandValidator().Validate(new RenameOrganizationCommand(new string('x', 129))).IsValid
        );
        Assert.True(
            new RenameOrganizationCommandValidator().Validate(new RenameOrganizationCommand(new string('x', 128))).IsValid
        );
    }
}
