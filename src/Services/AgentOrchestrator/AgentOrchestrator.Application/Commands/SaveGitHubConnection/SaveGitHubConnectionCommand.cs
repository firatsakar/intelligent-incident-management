using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.DTOs;
using AgentOrchestrator.Domain.Aggregates;
using BuildingBlocks.SharedKernel;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace AgentOrchestrator.Application.Commands.SaveGitHubConnection;

/// <summary>
/// Creates the organisation's connection or replaces its settings. <see cref="Token"/> blank keeps
/// the stored one — the form never has it to send back.
/// </summary>
public sealed record SaveGitHubConnectionCommand(
    string? Token,
    bool IsEnabled,
    IReadOnlyList<RepositoryMappingDto> Repositories
) : IRequest<GitHubConnectionDto>;

public sealed class SaveGitHubConnectionCommandHandler
    : IRequestHandler<SaveGitHubConnectionCommand, GitHubConnectionDto>
{
    private readonly IGitHubConnectionRepository _connections;
    private readonly IOrganizationContext _organization;

    public SaveGitHubConnectionCommandHandler(
        IGitHubConnectionRepository connections,
        IOrganizationContext organization
    )
    {
        _connections = connections;
        _organization = organization;
    }

    public async Task<GitHubConnectionDto> Handle(
        SaveGitHubConnectionCommand request,
        CancellationToken cancellationToken
    )
    {
        var repositories = request.Repositories.Select(r => r.ToDomain()).ToList();
        var connection = await _connections.GetAsync(cancellationToken);

        if (connection is null)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                throw new ValidationException(
                    [new ValidationFailure(nameof(request.Token), "A new connection needs a token.")]
                );

            connection = GitHubConnection.Create(
                _organization.Required,
                request.Token,
                repositories,
                request.IsEnabled
            );

            await _connections.AddAsync(connection, cancellationToken);
        }
        else
        {
            connection.Update(request.Token, repositories, request.IsEnabled);
        }

        await _connections.SaveChangesAsync(cancellationToken);

        return GitHubConnectionDto.FromDomain(connection);
    }
}
