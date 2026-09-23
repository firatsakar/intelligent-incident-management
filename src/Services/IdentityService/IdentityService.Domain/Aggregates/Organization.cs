using BuildingBlocks.SharedKernel;
using IdentityService.Domain.Events;

namespace IdentityService.Domain.Aggregates;

/// <summary>
/// The unit every other row in the platform belongs to. An incident is shared by a team, so the
/// owner of the data is the team, not the person who happened to open it.
/// </summary>
/// <remarks>
/// It holds almost nothing on purpose. The organisation's id is what travels — in a claim, in an
/// integration event, in a query filter — and the four other services store that id without ever
/// being able to read this table. A name that lived in two databases would be a name that could
/// disagree with itself.
/// </remarks>
public sealed class Organization : AggregateRoot
{
    private Organization() { }

    public string Name { get; private set; } = default!;

    public static Organization Create(string name)
    {
        var organization = new Organization { Id = Guid.NewGuid(), Name = name.Trim() };

        organization.AddDomainEvent(
            new OrganizationCreatedDomainEvent(organization.Id, organization.Name)
        );

        return organization;
    }

    public void Rename(string name)
    {
        Name = name.Trim();
        SetUpdatedAt();
    }
}
