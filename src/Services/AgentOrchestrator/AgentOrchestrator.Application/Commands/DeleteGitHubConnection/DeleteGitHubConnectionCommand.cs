using AgentOrchestrator.Application.Abstractions;
using MediatR;

namespace AgentOrchestrator.Application.Commands.DeleteGitHubConnection;

/// <summary>Removes the connection and its token. Idempotent: deleting nothing is not an error.</summary>
public sealed record DeleteGitHubConnectionCommand : IRequest;

public sealed class DeleteGitHubConnectionCommandHandler : IRequestHandler<DeleteGitHubConnectionCommand>
{
    private readonly IGitHubConnectionRepository _connections;

    public DeleteGitHubConnectionCommandHandler(IGitHubConnectionRepository connections)
    {
        _connections = connections;
    }

    public async Task Handle(DeleteGitHubConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connections.GetAsync(cancellationToken);

        if (connection is null)
            return;

        _connections.Remove(connection);
        await _connections.SaveChangesAsync(cancellationToken);
    }
}
