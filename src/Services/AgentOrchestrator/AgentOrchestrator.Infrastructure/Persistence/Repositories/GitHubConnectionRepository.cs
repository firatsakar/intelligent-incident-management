using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace AgentOrchestrator.Infrastructure.Persistence.Repositories;

public sealed class GitHubConnectionRepository : IGitHubConnectionRepository
{
    private readonly AgentDbContext _context;

    public GitHubConnectionRepository(AgentDbContext context)
    {
        _context = context;
    }

    // Through the organisation filter, which is what makes "the" connection the caller's own.
    public Task<GitHubConnection?> GetAsync(CancellationToken cancellationToken = default) =>
        _context.GitHubConnections.FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(GitHubConnection connection, CancellationToken cancellationToken = default) =>
        await _context.GitHubConnections.AddAsync(connection, cancellationToken);

    public void Remove(GitHubConnection connection) => _context.GitHubConnections.Remove(connection);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
