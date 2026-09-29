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
    // Null on outbox rows written before related changes existed.
    IReadOnlyList<RelatedChange>? RelatedChanges = null
) : DomainEvent;
