namespace AgentOrchestrator.Domain.ValueObjects;

/// <summary>
/// A commit the analysis named as a likely cause (Adım 17.5). Every field comes from what the code
/// host returned during that analysis — the model contributes only which sha — and
/// <see cref="Url"/> is built by the platform, never taken from model output.
/// </summary>
public sealed record RelatedChange
{
    public required string Sha { get; init; }
    public required string Title { get; init; }
    public string? Author { get; init; }
    public required DateTime CommittedAt { get; init; }
    public required string Url { get; init; }
}
