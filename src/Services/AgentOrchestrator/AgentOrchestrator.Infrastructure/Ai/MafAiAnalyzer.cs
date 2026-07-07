using System.Text.Json;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.ValueObjects;
using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestrator.Infrastructure.Ai;

public sealed class MafAiAnalyzer : IAiAnalyzer
{
    private readonly AIAgent _agent;
    private readonly AiAnalyzerOptions _options;
    private readonly ILogger<MafAiAnalyzer> _logger;

    public MafAiAnalyzer(IOptions<AiAnalyzerOptions> options, ILogger<MafAiAnalyzer> logger)
    {
        _options = options.Value;
        _logger = logger;

        var client = new AnthropicClient { ApiKey = _options.ApiKey };

        _agent = client.AsAIAgent(
            model: _options.Model,
            instructions: BuildInstructions(),
            name: "IncidentAnalyzer"
        );
    }

    public async Task<AnalysisResult> AnalyzeIncidentAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default
    )
    {
        var userPrompt = BuildUserPrompt(title, description);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _agent.RunAsync(userPrompt, cancellationToken: cancellationToken);
        stopwatch.Stop();

        var rawText = response.Text;

        _logger.LogInformation(
            "MAF agent response received ({Length} chars) in {DurationMs}ms end-to-end",
            rawText.Length,
            stopwatch.ElapsedMilliseconds
        );

        var metadata = new AnalysisMetadata
        {
            ModelName = _options.Model,
            InputTokens = (int)(response.Usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(response.Usage?.OutputTokenCount ?? 0),
            EndToEndDurationMs = stopwatch.ElapsedMilliseconds,
        };

        return ParseResponse(rawText, metadata);
    }

    private static string BuildInstructions()
    {
        return """
            You are an expert Site Reliability Engineer (SRE) analyzing production incidents.
            Given an incident's title and description, you must determine:
            1. The appropriate priority level
            2. The most likely category
            3. A brief technical reasoning
            4. Concrete remediation steps an on-call engineer should take, in order
            5. Your confidence in this analysis

            Confidence calibration rules:
            - 0.9-1.0: The description contains explicit technical evidence (error types, stack traces, metrics) that clearly points to one diagnosis.
            - 0.6-0.8: The evidence is suggestive but alternative explanations remain plausible.
            - 0.3-0.5: The description is vague or lacks technical detail; your analysis is largely inference.
            - Below 0.3: You are mostly guessing. Never inflate confidence to appear helpful.

            You must respond ONLY with a valid JSON object in this exact format, with no markdown, no code fences, no extra text:
            {
              "suggestedPriority": "Critical | High | Medium | Low",
              "suggestedCategory": "Database | Network | Application | Infrastructure | Security | Performance | Other",
              "reasoning": "A concise 1-2 sentence technical explanation.",
              "suggestedSteps": ["First remediation step", "Second remediation step"],
              "confidence": 0.85
            }

            Rules for suggestedSteps:
            - Provide 2 to 5 steps, ordered by execution priority.
            - Each step must be a single actionable instruction (start with a verb).
            - Steps must be specific to this incident, not generic advice like "check the logs".
            """;
    }

    private static string BuildUserPrompt(string title, string description)
    {
        return $"""
            Analyze the following incident:

            Title: {title}
            Description: {description}
            """;
    }

    private AnalysisResult ParseResponse(string rawText, AnalysisMetadata metadata)
    {
        try
        {
            var cleaned = rawText.Replace("```json", "").Replace("```", "").Trim();

            var parsed = JsonSerializer.Deserialize<AiResponseModel>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (parsed is null)
                throw new InvalidOperationException("AI response could not be parsed.");

            return new AnalysisResult
            {
                SuggestedPriority = parsed.SuggestedPriority,
                SuggestedCategory = parsed.SuggestedCategory,
                Reasoning = parsed.Reasoning,
                SuggestedSteps = parsed.SuggestedSteps,
                Confidence = parsed.Confidence,
                Metadata = metadata,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse MAF agent response as JSON: {RawText}", rawText);
            throw new InvalidOperationException(
                "AI returned a response that could not be parsed as valid JSON.",
                ex
            );
        }
    }

    private sealed record AiResponseModel
    {
        public string SuggestedPriority { get; init; } = default!;
        public string SuggestedCategory { get; init; } = default!;
        public string Reasoning { get; init; } = default!;
        public IReadOnlyList<string> SuggestedSteps { get; init; } = [];
        public double? Confidence { get; init; }
    }
}
