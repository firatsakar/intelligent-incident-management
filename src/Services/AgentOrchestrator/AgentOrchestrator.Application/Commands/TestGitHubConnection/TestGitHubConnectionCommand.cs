using AgentOrchestrator.Application.Abstractions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Application.Commands.TestGitHubConnection;

/// <summary>
/// Reads each mapped repository once with the stored token, the way an analysis would, and
/// reports per repository whether it worked. Nothing is written anywhere — including here.
/// </summary>
public sealed record TestGitHubConnectionCommand : IRequest<GitHubConnectionTestDto>;

public sealed record GitHubConnectionTestDto(IReadOnlyList<RepositoryCheckDto> Repositories);

/// <summary>
/// <see cref="RecentChanges"/> counts commits in the last week, as proof the repository was read
/// rather than merely reached. <see cref="Error"/> is GitHub's own sentence, or ours when it
/// could not be reached at all.
/// </summary>
public sealed record RepositoryCheckDto(
    string Service,
    string Repository,
    string? Branch,
    bool Ok,
    int? RecentChanges,
    string? Error
);

public sealed class TestGitHubConnectionCommandHandler
    : IRequestHandler<TestGitHubConnectionCommand, GitHubConnectionTestDto>
{
    private static readonly TimeSpan Window = TimeSpan.FromDays(7);

    private readonly IGitHubConnectionRepository _connections;
    private readonly IRepositoryChangeSourceFactory _sources;
    private readonly ILogger<TestGitHubConnectionCommandHandler> _logger;

    public TestGitHubConnectionCommandHandler(
        IGitHubConnectionRepository connections,
        IRepositoryChangeSourceFactory sources,
        ILogger<TestGitHubConnectionCommandHandler> logger
    )
    {
        _connections = connections;
        _sources = sources;
        _logger = logger;
    }

    public async Task<GitHubConnectionTestDto> Handle(
        TestGitHubConnectionCommand request,
        CancellationToken cancellationToken
    )
    {
        var connection =
            await _connections.GetAsync(cancellationToken)
            ?? throw new ValidationException(
                [new ValidationFailure("Connection", "Save a GitHub connection before testing it.")]
            );

        if (connection.Repositories.Count == 0)
            throw new ValidationException(
                [new ValidationFailure("Repositories", "Map at least one repository to test.")]
            );

        IRepositoryChangeSource source;

        try
        {
            source = await _sources.OpenAsync(connection.Token, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing past the connection works either, so every row says the same thing.
            _logger.LogWarning(ex, "Could not open the GitHub MCP session for a connection test.");

            return new GitHubConnectionTestDto(
                connection
                    .Repositories.Select(r => new RepositoryCheckDto(r.Service, r.FullName, r.Branch, false, null, ex.Message))
                    .ToList()
            );
        }

        await using (source)
        {
            var until = DateTime.UtcNow;
            var results = new List<RepositoryCheckDto>();

            foreach (var repository in connection.Repositories)
            {
                try
                {
                    var changes = await source.ListChangesAsync(repository, until - Window, until, 100, cancellationToken);

                    results.Add(new RepositoryCheckDto(repository.Service, repository.FullName, repository.Branch, true, changes.Count, null));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    results.Add(new RepositoryCheckDto(repository.Service, repository.FullName, repository.Branch, false, null, ex.Message));
                }
            }

            return new GitHubConnectionTestDto(results);
        }
    }
}
