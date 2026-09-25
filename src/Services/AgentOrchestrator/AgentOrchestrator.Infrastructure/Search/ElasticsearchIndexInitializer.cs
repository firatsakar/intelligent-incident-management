using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestrator.Infrastructure.Search;

public sealed class ElasticsearchIndexInitializer : IHostedService
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchIndexInitializer> _logger;

    public ElasticsearchIndexInitializer(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchIndexInitializer> logger
    )
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indexName = _options.AnalysesIndexName;

        var exists = await _client.Indices.ExistsAsync(indexName, cancellationToken);
        if (exists.Exists)
        {
            // An index created before organisations existed has no mapping for the field the
            // search now filters on, and dynamic mapping would give a guid a text type with a
            // keyword sub-field — against which the term filter silently matches nothing. Adding
            // a field to an existing mapping is allowed; changing one is not, so this is safe to
            // run on every start.
            var mapped = await _client.Indices.PutMappingAsync<AnalysisDocument>(
                indexName,
                m => m.Properties(p => p.Keyword(k => k.OrganizationId)),
                cancellationToken
            );

            _logger.LogInformation(
                "Elasticsearch index {IndexName} already exists; organisation mapping {Result}.",
                indexName,
                mapped.IsValidResponse ? "ensured" : "could not be ensured: " + mapped.DebugInformation
            );

            return;
        }

        var response = await _client.Indices.CreateAsync(
            indexName,
            c =>
                c.Mappings(m =>
                    m.Properties<AnalysisDocument>(p =>
                        p.Keyword(k => k.OrganizationId)
                            .Keyword(k => k.IncidentId)
                            .Text(t => t.Title, td => td.Analyzer("english"))
                            .Text(t => t.Description, td => td.Analyzer("english"))
                            .Keyword(k => k.SuggestedCategory)
                            .Keyword(k => k.SuggestedPriority)
                            .Text(t => t.Reasoning, td => td.Analyzer("english"))
                            .Keyword(k => k.SuggestedSteps)
                            .DoubleNumber(d => d.Confidence)
                            .Date(dt => dt.AnalyzedAt)
                    )
                ),
            cancellationToken
        );

        if (response.IsValidResponse)
        {
            _logger.LogInformation(
                "Elasticsearch index {IndexName} created with explicit mapping.",
                indexName
            );
        }
        else
        {
            _logger.LogError(
                "Failed to create Elasticsearch index {IndexName}. Reason: {Reason}",
                indexName,
                response.DebugInformation
            );
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
