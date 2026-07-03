using System.Text.Json;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.ValueObjects;
using Anthropic;
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
            InputTokens = 0,
            OutputTokens = 0,
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

            You must respond ONLY with a valid JSON object in this exact format, with no markdown, no code fences, no extra text:
            {
              "suggestedPriority": "Critical | High | Medium | Low",
              "suggestedCategory": "Database | Network | Application | Infrastructure | Security | Performance | Other",
              "reasoning": "A concise 1-2 sentence technical explanation."
            }
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
    }
}
