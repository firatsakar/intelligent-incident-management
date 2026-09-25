using BuildingBlocks.EventBus;

namespace BuildingBlocks.Contracts;

/// <summary>
/// An open incident was closed, and whoever closed it said whether it was a real problem.
///
/// This is how telemetry learns. The signature that opened the incident is released — so the
/// next burst of the same error opens a new incident rather than being absorbed into a closed
/// one — and the verdict is counted against it, which the promotion gate reads the next time
/// that signature fires.
///
/// Published once, on the way from open to closed; moving between Resolved and Closed afterwards
/// says nothing new. Verdict and Status are strings, as Severity is on the other contracts: the
/// enums belong to IncidentService, and a consumer should not have to reference its domain to
/// read a message.
/// </summary>
public sealed record IncidentResolvedEvent : IntegrationEvent
{
    public required Guid IncidentId { get; init; }

    /// <summary>Resolved or Closed.</summary>
    public required string Status { get; init; }

    /// <summary>Real or FalsePositive.</summary>
    public required string Verdict { get; init; }

    public required DateTime ResolvedAt { get; init; }
}
