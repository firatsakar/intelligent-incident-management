using BuildingBlocks.EventBus;

namespace BuildingBlocks.Contracts;

/// <summary>
/// An organisation now exists. Services that need something in place before it can be used do it
/// when this arrives.
/// </summary>
/// <remarks>
/// Today there is exactly one such thing, and it is the reason this event exists at all.
/// TelemetryIngestionService seeds one catch-all detection rule when its table is empty; once
/// rules belong to an organisation that row belongs to nobody, and an organisation without a rule
/// ingests logs, builds signatures and detects nothing — with no error anywhere to say so. The
/// service that must write the rule cannot read the table an organisation is born in, because
/// they are separate databases. This is how the fact crosses.
/// </remarks>
public sealed record OrganizationCreatedEvent : IntegrationEvent
{
    // No OrganizationId of its own: the base carries one, and for this event the organisation it
    // is about and the organisation it is scoped to are the same thing. Declaring it here again
    // would hide the base's and give a consumer two fields that could disagree.

    /// <summary>For logs and for naming whatever a consumer creates. Not an identifier.</summary>
    public required string Name { get; init; }
}
