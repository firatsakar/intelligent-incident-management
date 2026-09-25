using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Application.DTOs;

/// <summary>
/// The connection as the settings screen reads it. The token is not here — not masked, not
/// shortened: absent. <see cref="HasToken"/> is everything a form needs to say "a token is saved".
/// </summary>
public sealed record GitHubConnectionDto
{
    public required bool IsConfigured { get; init; }
    public required bool HasToken { get; init; }
    public required bool IsEnabled { get; init; }
    public required IReadOnlyList<RepositoryMappingDto> Repositories { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static GitHubConnectionDto NotConfigured { get; } =
        new()
        {
            IsConfigured = false,
            HasToken = false,
            IsEnabled = false,
            Repositories = [],
        };

    public static GitHubConnectionDto FromDomain(GitHubConnection connection) =>
        new()
        {
            IsConfigured = true,
            HasToken = !string.IsNullOrEmpty(connection.Token),
            IsEnabled = connection.IsEnabled,
            Repositories = connection.Repositories.Select(RepositoryMappingDto.FromDomain).ToList(),
            UpdatedAt = connection.UpdatedAt ?? connection.CreatedAt,
        };
}

public sealed record RepositoryMappingDto(string Service, string Owner, string Repository, string? Branch)
{
    public static RepositoryMappingDto FromDomain(RepositoryMapping mapping) =>
        new(mapping.Service, mapping.Owner, mapping.Repository, mapping.Branch);

    public RepositoryMapping ToDomain() =>
        new(
            Service.Trim(),
            Owner.Trim(),
            Repository.Trim(),
            string.IsNullOrWhiteSpace(Branch) ? null : Branch.Trim()
        );
}
