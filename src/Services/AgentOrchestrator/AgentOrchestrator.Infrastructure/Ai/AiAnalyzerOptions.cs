namespace AgentOrchestrator.Infrastructure.Ai;

public sealed class AiAnalyzerOptions
{
    public const string SectionName = "AiAnalyzer";

    public string ApiKey { get; set; } = default!;
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 1024;
}
