using AgentOrchestrator.Domain.ValueObjects;
using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.Events;

public sealed record IncidentAnalysisCompletedDomainEvent(
    Guid IncidentId,
    string IncidentTitle,
    string IncidentDescription,
    string SuggestedCategory,
    string SuggestedPriority,
    string Reasoning,
    double? Confidence,
    // Null on rows written before Adım 17.5, still in the outbox when it deployed.
    IReadOnlyList<RelatedChange>? RelatedChanges = null
) : DomainEvent;
