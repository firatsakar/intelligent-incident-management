using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.Events;

/// <summary>
/// Raised when an analysis ends without a result.
///
/// MarkAsCompleted has always raised its counterpart; MarkAsFailed raised nothing, which made
/// failure the one outcome that never left this service. The interceptor harvests this into the
/// outbox in the same transaction as the status change, so the failure and the message it
/// produces commit together or not at all — the same guarantee success already had.
/// </summary>
public sealed record IncidentAnalysisFailedDomainEvent(
    Guid IncidentId,
    string IncidentTitle,
    string ErrorMessage
) : DomainEvent;
