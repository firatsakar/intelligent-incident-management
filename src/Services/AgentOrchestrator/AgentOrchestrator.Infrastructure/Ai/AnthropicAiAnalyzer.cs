using System.Text.Json;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.ValueObjects;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestrator.Infrastructure.Ai;

public sealed class AnthropicAiAnalyzer : IAiAnalyzer
{
    private readonly AnthropicClient _client;
    private readonly AiAnalyzerOptions _options;
    private readonly ILogger<AnthropicAiAnalyzer> _logger;

    public AnthropicAiAnalyzer(
        IOptions<AiAnalyzerOptions> options,
        ILogger<AnthropicAiAnalyzer> logger
    )
    {
        _options = options.Value;
        _client = new AnthropicClient(_options.ApiKey);
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeIncidentAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default
    )
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(title, description);

        var parameters = new MessageParameters
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            Temperature = 0.0m,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, userPrompt)],
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

        var rawText = response.Content.OfType<TextContent>().Last().Text;

        _logger.LogInformation("AI raw response received ({Length} chars)", rawText.Length);

        return ParseResponse(rawText);
    }

    private static string BuildSystemPrompt()
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

    private AnalysisResult ParseResponse(string rawText)
    {
        try
        {
            // be sure to remove any code fences or markdown that might be present in the AI response
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
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI response as JSON: {RawText}", rawText);
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
