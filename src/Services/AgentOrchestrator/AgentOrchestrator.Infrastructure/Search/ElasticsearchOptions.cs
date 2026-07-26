namespace AgentOrchestrator.Infrastructure.Search;

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Uri { get; init; } = "http://localhost:9200";

    public string AnalysesIndexName { get; init; } = "incident-analyses";
}
