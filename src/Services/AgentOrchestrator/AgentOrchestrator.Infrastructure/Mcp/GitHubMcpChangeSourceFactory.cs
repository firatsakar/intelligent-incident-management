using System.Text.Json;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Changes;
using AgentOrchestrator.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AgentOrchestrator.Infrastructure.Mcp;

/// <summary>
/// GitHub's own MCP server, reached with the organisation's token (Adım 17.5). MCP because this is
/// somebody else's system: the protocol is the one GitHub publishes and maintains, and the next
/// source (Grafana, Kubernetes) plugs into the same client rather than into a second REST wrapper.
/// </summary>
/// <remarks>
/// <para>
/// Read-only twice over: the endpoint path selects the server's read-only mode — confirmed live,
/// the catalogue it offers has no tool that writes — and <see cref="IRepositoryChangeSource"/> has
/// no member that could call anything but <c>list_commits</c> and <c>get_commit</c>. The model
/// never sees the MCP catalogue at all; it sees the two tools <c>ChangeTools</c> wraps.
/// </para>
/// <para>
/// Discovery (Adım 17.5 Parça 3, against the live server with a real token): the
/// <c>X-MCP-Tools</c> header did not narrow the catalogue on the <c>/x/repos/readonly</c> path —
/// the whole read-only repository toolset was listed. It is still sent, as intent; nothing relies
/// on it.
/// </para>
/// </remarks>
public sealed class GitHubMcpChangeSourceFactory : IRepositoryChangeSourceFactory
{
    public const string HttpClientName = "github-mcp";

    internal const string ListCommits = "list_commits";
    internal const string GetCommit = "get_commit";

    // The catalogue is logged once per process: it is how the real tool shapes were learned
    // (discovery-first), and how a change on GitHub's side would be noticed.
    private static int _catalogueLogged;

    private readonly IHttpClientFactory _httpClients;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GitHubMcpChangeSourceFactory> _logger;
    private readonly GitHubMcpOptions _options;

    public GitHubMcpChangeSourceFactory(
        IHttpClientFactory httpClients,
        ILoggerFactory loggerFactory,
        IOptions<GitHubMcpOptions> options
    )
    {
        _httpClients = httpClients;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GitHubMcpChangeSourceFactory>();
        _options = options.Value;
    }

    public async Task<IRepositoryChangeSource> OpenAsync(string token, CancellationToken cancellationToken = default)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_options.Endpoint),
                Name = "github",
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    // The only place the token goes. Headers are not recorded by the HttpClient
                    // instrumentation or its logging, and the token is never put in a message.
                    ["Authorization"] = $"Bearer {token}",
                    ["X-MCP-Readonly"] = "true",
                    // Intent only: the server ignored it on this path (see remarks).
                    ["X-MCP-Tools"] = $"{ListCommits},{GetCommit}",
                },
            },
            _httpClients.CreateClient(HttpClientName),
            _loggerFactory,
            ownsHttpClient: false
        );

        using var timeout = GitHubMcpChangeSource.Timeout(_options, cancellationToken);

        var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions { ClientInfo = new Implementation { Name = "iim-agent-orchestrator", Version = "1.0.0" } },
            _loggerFactory,
            timeout.Token
        );

        try
        {
            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);

            if (Interlocked.Exchange(ref _catalogueLogged, 1) == 0)
            {
                foreach (var tool in tools)
                    _logger.LogInformation(
                        "GitHub MCP offers {Tool}: {Schema}",
                        tool.Name,
                        tool.JsonSchema.GetRawText()
                    );
            }

            foreach (var required in new[] { ListCommits, GetCommit })
            {
                if (tools.All(tool => tool.Name != required))
                    throw new InvalidOperationException($"GitHub's MCP server does not offer {required} at {_options.Endpoint}.");
            }

            return new GitHubMcpChangeSource(client, _options, _logger);
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }
}

internal sealed class GitHubMcpChangeSource : IRepositoryChangeSource
{
    private readonly McpClient _client;
    private readonly GitHubMcpOptions _options;
    private readonly ILogger _logger;

    public GitHubMcpChangeSource(McpClient client, GitHubMcpOptions options, ILogger logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public static CancellationTokenSource Timeout(GitHubMcpOptions options, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        return source;
    }

    public async Task<IReadOnlyList<RecentChange>> ListChangesAsync(
        RepositoryMapping repository,
        DateTime since,
        DateTime until,
        int max,
        CancellationToken cancellationToken = default
    )
    {
        var arguments = new Dictionary<string, object?>
        {
            ["owner"] = repository.Owner,
            ["repo"] = repository.Repository,
            ["since"] = since.ToUniversalTime().ToString("O"),
            ["until"] = until.ToUniversalTime().ToString("O"),
            ["perPage"] = Math.Clamp(max, 1, 100),
            // What the list is read for and nothing else: no committer, no parents, no URLs the
            // platform would not use anyway (it builds its own).
            ["fields"] = new[] { "sha", "commit", "author" },
        };

        // Null is the default branch, which GitHub picks when sha is absent.
        if (repository.Branch is not null)
            arguments["sha"] = repository.Branch;

        var text = await CallAsync(GitHubMcpChangeSourceFactory.ListCommits, arguments, cancellationToken);

        return Parse(
                GitHubMcpChangeSourceFactory.ListCommits,
                text,
                GitHubResponses.ParseCommits
            )
            .OrderByDescending(change => change.CommittedAt)
            .Take(max)
            .ToList();
    }

    public async Task<ChangeDetail?> GetChangeAsync(
        RepositoryMapping repository,
        string sha,
        CancellationToken cancellationToken = default
    )
    {
        var arguments = new Dictionary<string, object?>
        {
            ["owner"] = repository.Owner,
            ["repo"] = repository.Repository,
            ["sha"] = sha,
            // The server's default is "stats": file names and line counts, no diff — which would
            // leave the analysis guessing what a change did. The diff is trimmed on our side.
            ["detail"] = "full_patch",
        };

        var text = await CallAsync(GitHubMcpChangeSourceFactory.GetCommit, arguments, cancellationToken);

        return Parse(GitHubMcpChangeSourceFactory.GetCommit, text, GitHubResponses.ParseCommit);
    }

    private async Task<string> CallAsync(
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken
    )
    {
        using var timeout = Timeout(_options, cancellationToken);

        var result = await _client.CallToolAsync(tool, arguments, cancellationToken: timeout.Token);
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

        // GitHub's own sentence ("Not Found", "Bad credentials"), shortened. It says which
        // repository or permission is wrong, which is exactly what the Admin needs to see.
        if (result.IsError == true)
            throw new InvalidOperationException(text.Length > 300 ? text[..300] : text);

        return text;
    }

    private T Parse<T>(string tool, string text, Func<string, T> parse)
    {
        try
        {
            return parse(text);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            // The shape, not the content: what came back is the customer's commit data.
            _logger.LogWarning(
                "GitHub MCP {Tool} answered in an unexpected shape ({Length} chars, starts with '{Start}').",
                tool,
                text.Length,
                text.Length > 1 ? text[..1] : text
            );

            throw new InvalidOperationException($"GitHub answered {tool} in a shape this platform does not read.", ex);
        }
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
