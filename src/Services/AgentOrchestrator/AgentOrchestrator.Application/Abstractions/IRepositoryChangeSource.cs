using AgentOrchestrator.Application.Changes;
using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Application.Abstractions;

/// <summary>
/// Opens a read-only view of an organisation's code host with that organisation's token
/// (Adım 17.5). Implemented over GitHub's MCP server; nothing above Infrastructure knows that.
/// </summary>
public interface IRepositoryChangeSourceFactory
{
    Task<IRepositoryChangeSource> OpenAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// One open session — for one analysis, or one press of the Test button. Only the two reads the
/// analysis needs exist here: nothing on this interface can write, so nothing built on it can.
/// </summary>
public interface IRepositoryChangeSource : IAsyncDisposable
{
    /// <summary>Commits to <paramref name="repository"/> between the two instants, newest first.</summary>
    Task<IReadOnlyList<RecentChange>> ListChangesAsync(
        RepositoryMapping repository,
        DateTime since,
        DateTime until,
        int max,
        CancellationToken cancellationToken = default
    );

    /// <summary>One commit's message and trimmed diff, or null when the host does not know it.</summary>
    Task<ChangeDetail?> GetChangeAsync(
        RepositoryMapping repository,
        string sha,
        CancellationToken cancellationToken = default
    );
}
