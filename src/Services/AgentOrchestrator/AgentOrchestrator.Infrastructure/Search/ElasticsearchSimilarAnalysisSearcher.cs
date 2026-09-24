using AgentOrchestrator.Application.DTOs;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestrator.Infrastructure.Search;

public sealed class ElasticsearchSimilarAnalysisSearcher : ISimilarAnalysisSearcher
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchSimilarAnalysisSearcher> _logger;

    public ElasticsearchSimilarAnalysisSearcher(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchSimilarAnalysisSearcher> logger
    )
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SimilarAnalysis>> SearchAsync(
        string query,
        Guid organizationId,
        Guid? excludeIncidentId = null,
        int maxResults = 3,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var response = await _client.SearchAsync<AnalysisDocument>(
            s =>
                s.Indices(_options.AnalysesIndexName)
                    .Size(maxResults)
                    .Query(q =>
                        q.Bool(b =>
                        {
                            b.Must(m =>
                                m.MultiMatch(mm =>
                                    mm.Query(query)
                                        .Fields(new[] { "title^2", "description", "reasoning" })
                                )
                            );

                            // Filter context, not query context: it decides what may match and
                            // contributes nothing to the score. Postgres's query filter does not
                            // reach this index — it is a separate store — so this clause is the
                            // only thing standing between one organisation's history and another's
                            // model.
                            b.Filter(f =>
                                f.Term(t =>
                                    t.Field("organizationId").Value(organizationId.ToString())
                                )
                            );

                            if (excludeIncidentId.HasValue)
                            {
                                b.MustNot(mn =>
                                    mn.Term(t =>
                                        t.Field("incidentId")
                                            .Value(excludeIncidentId.Value.ToString())
                                    )
                                );
                            }
                        })
                    ),
            cancellationToken
        );

        if (!response.IsValidResponse)
        {
            _logger.LogWarning(
                "Similar-analysis search failed for query {Query}. Reason: {Reason}",
                query,
                response.DebugInformation
            );

            // Search is a best-effort enrichment, not a source of truth.
            // Returning empty degrades the analysis gracefully instead of failing it.
            return [];
        }

        var results = response
            .Hits.Where(h => h.Source is not null)
            .Select(h => new SimilarAnalysis
            {
                IncidentId = h.Source!.IncidentId,
                Title = h.Source.Title,
                SuggestedCategory = h.Source.SuggestedCategory,
                SuggestedPriority = h.Source.SuggestedPriority,
                Reasoning = h.Source.Reasoning,
                SuggestedSteps = h.Source.SuggestedSteps,
                Confidence = h.Source.Confidence ?? default,
                AnalyzedAt = h.Source.AnalyzedAt,
                MatchScore = h.Score ?? 0,
            })
            .ToList();

        _logger.LogInformation(
            "Similar-analysis search for {Query} returned {Count} results.",
            query,
            results.Count
        );

        return results;
    }
}
