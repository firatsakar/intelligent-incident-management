using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Events;

namespace IdentityService.Tests;

public sealed class OrganizationTests
{
    [Fact]
    public void CreatingTrimsTheName()
    {
        Assert.Equal("Acme Operations", Organization.Create("  Acme Operations  ").Name);
    }

    // TelemetryIngestionService cannot read this table — separate database — so an organisation
    // that exists without having announced itself gets no detection rule, ingests logs, and
    // silently detects nothing. The event is raised by the factory rather than by a handler so
    // there is no way to create one without it.
    [Fact]
    public void CreatingAnnouncesItself()
    {
        var organization = Organization.Create("Acme Operations");

        var raised = Assert.Single(organization.DomainEvents);
        var created = Assert.IsType<OrganizationCreatedDomainEvent>(raised);

        Assert.Equal(organization.Id, created.OrganizationId);
        Assert.Equal("Acme Operations", created.Name);
    }

    [Fact]
    public void RenamingDoesNotAnnounceAnythingNew()
    {
        var organization = Organization.Create("Acme Operations");
        organization.ClearDomainEvents();

        organization.Rename("Acme Platform");

        Assert.Equal("Acme Platform", organization.Name);
        Assert.Empty(organization.DomainEvents);
    }
}
