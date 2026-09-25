using AgentOrchestrator.Domain.Aggregates;

namespace AgentOrchestrator.Application.Abstractions;

/// <summary>
/// The organisation's GitHub connection — at most one, found through the organisation filter, so
/// there is no id to ask for and no way to ask for another organisation's.
/// </summary>
public interface IGitHubConnectionRepository
{
    Task<GitHubConnection?> GetAsync(CancellationToken cancellationToken = default);

    Task AddAsync(GitHubConnection connection, CancellationToken cancellationToken = default);

    void Remove(GitHubConnection connection);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
