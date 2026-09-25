using BuildingBlocks.SharedKernel;
using IncidentService.Domain.Enums;

namespace IncidentService.Domain.Events;

/// <summary>
/// An open incident was closed, with a verdict. Raised once, on the way from open to closed —
/// moving between Resolved and Closed afterwards is bookkeeping, not a second conclusion.
/// </summary>
public sealed record IncidentResolvedDomainEvent : DomainEvent
{
    public required Guid IncidentId { get; init; }
    public required IncidentStatus Status { get; init; }
    public required IncidentVerdict Verdict { get; init; }
    public required DateTime ResolvedAt { get; init; }
}
