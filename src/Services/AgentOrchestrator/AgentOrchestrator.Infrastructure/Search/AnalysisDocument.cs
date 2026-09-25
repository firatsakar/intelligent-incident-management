using System.Text.Json.Serialization;

namespace AgentOrchestrator.Infrastructure.Search;

public sealed record AnalysisDocument
{
    [JsonPropertyName("organizationId")]
    public required Guid OrganizationId { get; init; }

    [JsonPropertyName("incidentId")]
    public required Guid IncidentId { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("suggestedCategory")]
    public string? SuggestedCategory { get; init; }

    [JsonPropertyName("suggestedPriority")]
    public string? SuggestedPriority { get; init; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; init; }

    [JsonPropertyName("suggestedSteps")]
    public IReadOnlyList<string> SuggestedSteps { get; init; } = [];

    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }

    [JsonPropertyName("analyzedAt")]
    public required DateTimeOffset AnalyzedAt { get; init; }
}
