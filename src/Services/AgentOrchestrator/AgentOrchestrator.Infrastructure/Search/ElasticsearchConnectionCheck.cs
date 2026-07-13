using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Infrastructure.Search;

public sealed class ElasticsearchConnectionCheck : IHostedService
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchConnectionCheck> _logger;

    public ElasticsearchConnectionCheck(
        ElasticsearchClient client,
        ILogger<ElasticsearchConnectionCheck> logger
    )
    {
        _client = client;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var response = await _client.PingAsync(cancellationToken);

        if (response.IsValidResponse)
        {
            _logger.LogInformation(
                "Elasticsearch connection established. Cluster is reachable at ping."
            );
        }
        else
        {
            _logger.LogWarning(
                "Elasticsearch ping failed. Search features will be degraded. Reason: {Reason}",
                response.DebugInformation
            );
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
