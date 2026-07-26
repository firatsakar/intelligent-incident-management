using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Aggregates;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestrator.Infrastructure.Search;

public sealed class ElasticsearchAnalysisIndexer : IAnalysisIndexer
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchAnalysisIndexer> _logger;

    public ElasticsearchAnalysisIndexer(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchAnalysisIndexer> logger
    )
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task IndexAsync(
        IncidentAnalysis analysis,
        CancellationToken cancellationToken = default
    )
    {
        var document = AnalysisDocumentMapper.ToDocument(analysis);
        var response = await _client.IndexAsync(
            document,
            idx => idx.Index(_options.AnalysesIndexName).Id(document.IncidentId.ToString()),
            cancellationToken
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Failed to index analysis for incident {IncidentId}. Reason: {Reason}",
                document.IncidentId,
                response.DebugInformation
            );

            throw new InvalidOperationException(
                $"Elasticsearch indexing failed for incident {document.IncidentId}."
            );
        }

        _logger.LogInformation(
            "Indexed analysis for incident {IncidentId} into {IndexName}. Result: {Result}",
            document.IncidentId,
            _options.AnalysesIndexName,
            response.Result
        );
    }

    public async Task IndexManyAsync(
        IReadOnlyCollection<IncidentAnalysis> analyses,
        CancellationToken cancellationToken = default
    )
    {
        if (analyses.Count == 0)
        {
            _logger.LogInformation("No analyses to index. Skipping bulk operation.");
            return;
        }

        var response = await _client.BulkAsync(
            b =>
                b.Index(_options.AnalysesIndexName)
                    .IndexMany(
                        analyses.Select(AnalysisDocumentMapper.ToDocument),
                        (descriptor, doc) => descriptor.Id(doc.IncidentId.ToString())
                    ),
            cancellationToken
        );

        if (response.Errors)
        {
            foreach (var item in response.ItemsWithErrors)
            {
                _logger.LogError(
                    "Bulk indexing failed for document {DocumentId}. Reason: {Reason}",
                    item.Id,
                    item.Error?.Reason
                );
            }

            throw new InvalidOperationException("Bulk indexing completed with errors.");
        }

        _logger.LogInformation(
            "Bulk indexed {Count} analyses into {IndexName}.",
            analyses.Count,
            _options.AnalysesIndexName
        );
    }
}
