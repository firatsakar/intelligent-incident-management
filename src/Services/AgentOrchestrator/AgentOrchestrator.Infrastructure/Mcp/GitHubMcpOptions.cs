namespace AgentOrchestrator.Infrastructure.Mcp;

/// <summary>Where GitHub's MCP server answers, and how long a call may take.</summary>
public sealed class GitHubMcpOptions
{
    public const string SectionName = "GitHubMcp";

    /// <summary>
    /// GitHub's hosted server, narrowed on the path to the repository toolset in read-only mode:
    /// the server itself then offers no tool that writes (confirmed live). The client calls only
    /// two of the tools it does offer (see <see cref="GitHubMcpChangeSourceFactory"/>).
    /// </summary>
    public string Endpoint { get; set; } = "https://api.githubcopilot.com/mcp/x/repos/readonly";

    /// <summary>Per call. An analysis waits on this, so a slow GitHub costs it seconds, not minutes.</summary>
    public int TimeoutSeconds { get; set; } = 15;
}
