using BuildingBlocks.SharedKernel;

namespace IncidentService.Domain.Events;

public sealed record IncidentCreatedDomainEvent : DomainEvent
{
    public required Guid IncidentId { get; init; }
    public required string Title { get; init; }
}
