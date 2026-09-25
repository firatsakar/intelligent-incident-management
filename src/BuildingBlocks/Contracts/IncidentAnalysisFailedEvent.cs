using BuildingBlocks.EventBus;

namespace BuildingBlocks.Contracts;

/// <summary>
/// The analysis was attempted and did not produce a result.
///
/// Until this existed, a failed analysis was silent end to end: MarkAsFailed raised no domain
/// event, so nothing reached the outbox, nothing reached IncidentService, and nothing reached a
/// screen. The incident sat on "analysis pending" forever — the one wrong state that never
/// corrects itself, because the thing that would have corrected it is what failed.
///
/// Deliberately not a variant of IncidentAnalyzedEvent. That event's consumers apply a priority
/// and a category; this one has neither, and a required field carrying a sentinel is how a
/// consumer ends up writing a sentinel into the incident.
/// </summary>
public sealed record IncidentAnalysisFailedEvent : IntegrationEvent
{
    public required Guid IncidentId { get; init; }
    public required string IncidentTitle { get; init; }

    /// <summary>
    /// What went wrong, in the provider's own words. Shown to an operator, so it is the reason
    /// rather than a category: "rate limit exceeded" and "invalid API key" call for different
    /// actions and a shared label would hide that.
    /// </summary>
    public required string Error { get; init; }
}
