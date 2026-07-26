using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.Events;

public sealed record IncidentAnalysisCompletedDomainEvent(
    Guid IncidentId,
    string IncidentTitle,
    string IncidentDescription,
    string SuggestedCategory,
    string SuggestedPriority,
    string Reasoning,
    double? Confidence
) : DomainEvent;
