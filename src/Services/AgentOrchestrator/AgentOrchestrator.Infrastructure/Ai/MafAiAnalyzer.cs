using System.Text.Json;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.ValueObjects;
using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestrator.Infrastructure.Ai;

public sealed class MafAiAnalyzer : IAiAnalyzer
{
    // TelemetryConstants.ActivitySources.AgentOrchestrator, which the platform's tracing listens
    // to. Spelled out rather than referenced: Observability brings the Serilog and OpenTelemetry
    // SDKs, and infrastructure needs neither to emit a span.
    private const string AgentActivitySource = "AgentOrchestrator";

    private readonly ISimilarAnalysisSearcher _searcher;
    private readonly AnthropicClient _client;
    private readonly AiAnalyzerOptions _options;
    private readonly ILogger<MafAiAnalyzer> _logger;

    public MafAiAnalyzer(
        IOptions<AiAnalyzerOptions> options,
        ILogger<MafAiAnalyzer> logger,
        ISimilarAnalysisSearcher searcher
    )
    {
        _options = options.Value;
        _logger = logger;
        _client = new AnthropicClient { ApiKey = _options.ApiKey };
        _searcher = searcher;
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        Guid organizationId,
        Guid incidentId,
        string title,
        string description,
        CancellationToken cancellationToken = default
    )
    {
        var userPrompt = BuildUserPrompt(title, description);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var agent = _client
            .AsAIAgent(
                model: _options.Model,
                instructions: BuildInstructions(),
                tools: [BuildSearchTool(organizationId, incidentId)]
            )
            .AsBuilder()
            // An invoke_agent span, a chat span per model turn, and an execute_tool span per
            // search the model decides to run — which is the part of an analysis nothing else
            // shows: what it looked for, in what order, and how long each step took. Wired below
            // function invocation by MAF itself, which is what makes the tool spans exist at all.
            .UseOpenTelemetry(
                AgentActivitySource,
                telemetry =>
                    // Metadata only: model, token counts, durations, tool names. The prompt is the
                    // customer's incident text and the response is the analysis; neither belongs
                    // in the platform's own trace store, which has no organisation boundary.
                    telemetry.EnableSensitiveData = false
            )
            .Build();
        var response = await agent.RunAsync(userPrompt, cancellationToken: cancellationToken);
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

    // The model supplies the query and nothing else. Both the organisation and the incident to
    // exclude are fixed in the closure: the model searches its own hypothesis, but it cannot choose
    // whose history it searches. Before this, the history was everybody's — and the leak would
    // have surfaced as prose in the reasoning, quoting another organisation's incident as
    // precedent, which is the least visible place a leak can land.
    private AIFunction BuildSearchTool(Guid organizationId, Guid currentIncidentId)
    {
        return AIFunctionFactory.Create(
            async (string query) =>
            {
                var results = await _searcher.SearchAsync(
                    query,
                    organizationId,
                    excludeIncidentId: currentIncidentId,
                    maxResults: 3
                );

                _logger.LogInformation(
                    "AI invoked similar-incident search. Query: {Query}, IncidentId: {IncidentId}, Results: {Count}",
                    query,
                    currentIncidentId,
                    results.Count
                );

                if (results.Count == 0)
                    return "No similar past incidents found.";

                return JsonSerializer.Serialize(
                    results.Select(r => new
                    {
                        r.IncidentId,
                        r.Title,
                        r.SuggestedCategory,
                        r.SuggestedPriority,
                        r.Reasoning,
                        r.SuggestedSteps,
                        r.Confidence,
                        MatchScore = Math.Round(r.MatchScore, 2),
                        AnalyzedAt = r.AnalyzedAt.ToString("yyyy-MM-dd"),
                    })
                );
            },
            name: "search_similar_incidents",
            description: "Searches the history of previously analyzed incidents for cases resembling a given query. "
                + "Use this to check whether this kind of failure has occurred before and what was done about it. "
                + "Pass a short, focused query describing the SUSPECTED ROOT CAUSE or the most distinctive error "
                + "signal (e.g. 'Npgsql connection pool exhausted'), NOT the full incident text and NOT generic "
                + "symptoms like '503' or 'service down'. You may call this more than once with different hypotheses. "
                + "Results include a matchScore (BM25 relevance, higher is better) — treat it as a hint, not a verdict; "
                + "judge relevance yourself by reading each result."
        );
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

            INVESTIGATION PROCESS:
            Before finalizing your analysis, form a hypothesis about the root cause from the
            incident text, then call search_similar_incidents with that hypothesis to check
            whether this system has seen the same failure before. Search with the distinctive
            technical signal (exception type, resource, subsystem), not the user-visible symptom.
            If your first search returns nothing useful, you may reformulate and search again.
            Skip the search only if the incident is too vague to form any hypothesis.

            USING SEARCH RESULTS:
            - Read each result and decide yourself whether it is genuinely the same class of
              failure. A high matchScore with a different root cause is NOT a match.
            - If a past incident matches, reference it explicitly in your reasoning.
            - Prefer remediation steps proven in this system over generic best practice.
            - Older records may have empty suggestedSteps or a confidence of 0. This means the
              field did not exist when they were analyzed — treat it as unknown, not low confidence.

            CONFIDENCE CALIBRATION (evidence-based):
            - 0.9-1.0: explicit error signal AND at least one genuinely matching past incident.
            - 0.6-0.8: clear error signal but no past precedent, or a partial match.
            - 0.3-0.5: ambiguous symptoms, no precedent found.
            - Below 0.3: insufficient information.
            Never inflate confidence. Finding no similar past incident should LOWER confidence.
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
            var start = cleaned.IndexOf('{');
            var end = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                cleaned = cleaned.Substring(start, end - start + 1);
            }

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
