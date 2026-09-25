using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.DTOs;
using MediatR;

namespace AgentOrchestrator.Application.Queries.GetGitHubConnection;

public sealed record GetGitHubConnectionQuery : IRequest<GitHubConnectionDto>;

// Not configured is an answer, not a 404: the settings screen asks whether there is a connection,
// and "no" is the normal state of every organisation until its Admin sets one up.
public sealed class GetGitHubConnectionQueryHandler
    : IRequestHandler<GetGitHubConnectionQuery, GitHubConnectionDto>
{
    private readonly IGitHubConnectionRepository _connections;

    public GetGitHubConnectionQueryHandler(IGitHubConnectionRepository connections)
    {
        _connections = connections;
    }

    public async Task<GitHubConnectionDto> Handle(
        GetGitHubConnectionQuery request,
        CancellationToken cancellationToken
    )
    {
        var connection = await _connections.GetAsync(cancellationToken);

        return connection is null
            ? GitHubConnectionDto.NotConfigured
            : GitHubConnectionDto.FromDomain(connection);
    }
}
