namespace NotificationService.Application.DTOs;

// What every channel renders: the analysis result, flattened and channel-agnostic.
public sealed record NotificationMessage
{
    public required Guid IncidentId { get; init; }
    public required string IncidentTitle { get; init; }
    public required string SuggestedPriority { get; init; }
    public required string SuggestedCategory { get; init; }
    public required string Reasoning { get; init; }
    public double? Confidence { get; init; }
    public required DateTime AnalyzedAt { get; init; }
}
