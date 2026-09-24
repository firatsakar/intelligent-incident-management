namespace BuildingBlocks.SharedKernel;

/// <summary>
/// Which organisation the work in flight belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Scoped, and with exactly one writer per scope. Over HTTP that writer is the middleware that
/// reads the claim; in the seven consumers that have no <c>HttpContext</c> — the outbox
/// dispatcher, the telemetry poller, the three event-bus subscribers — it is whatever unpacks the
/// message. A handler that sets this itself has decided who it is working for, which is the one
/// thing it must never do.
/// </para>
/// <para>
/// In SharedKernel rather than in a web or application package because of who has to see it: a
/// DbContext applies it as a query filter, an application handler stamps it onto a new row, and a
/// middleware fills it in. SharedKernel is the only assembly all three already reference.
/// </para>
/// </remarks>
public interface IOrganizationContext
{
    /// <summary>
    /// The organisation, or null where nothing has established one — an unauthenticated request,
    /// or a background sweep that is deliberately not scoped to anybody.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// The organisation, refusing rather than guessing. For the paths where proceeding without one
    /// would mean writing a row nobody owns or reading rows belonging to everybody.
    /// </summary>
    Guid Required { get; }

    void Set(Guid organizationId);
}

public sealed class OrganizationContext : IOrganizationContext
{
    public Guid? OrganizationId { get; private set; }

    public Guid Required =>
        OrganizationId
        ?? throw new InvalidOperationException(
            "No organisation in scope. Over HTTP this means the request was not authenticated; "
                + "in a background consumer it means the message did not carry one."
        );

    public void Set(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "An empty organisation id is not a scope.",
                nameof(organizationId)
            );

        OrganizationId = organizationId;
    }
}
