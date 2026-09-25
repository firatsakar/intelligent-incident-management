using AgentOrchestrator.Domain.ValueObjects;
using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.Aggregates;

/// <summary>
/// An organisation's GitHub, as the analysis may read it (Adım 17.5): a read-only token and which
/// repository each service's code lives in. One per organisation, configured by its Admins.
/// </summary>
/// <remarks>
/// The token is the organisation's credential for its own code. It leaves this service only in
/// the Authorization header of a call to GitHub — never in a response, a log line or a span.
/// </remarks>
public sealed class GitHubConnection : AggregateRoot
{
    private List<RepositoryMapping> _repositories = [];

    private GitHubConnection() { }

    public Guid OrganizationId { get; private set; }

    public string Token { get; private set; } = default!;

    public bool IsEnabled { get; private set; }

    public IReadOnlyList<RepositoryMapping> Repositories => _repositories;

    public static GitHubConnection Create(
        Guid organizationId,
        string token,
        IEnumerable<RepositoryMapping> repositories,
        bool isEnabled
    )
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("A new connection needs a token.", nameof(token));

        return new GitHubConnection
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Token = token.Trim(),
            _repositories = repositories.ToList(),
            IsEnabled = isEnabled,
        };
    }

    /// <summary>
    /// A blank token keeps the stored one: the form never receives the token back, so a save that
    /// did not touch the field must not erase the credential nobody can retype from the screen.
    /// </summary>
    public void Update(string? token, IEnumerable<RepositoryMapping> repositories, bool isEnabled)
    {
        if (!string.IsNullOrWhiteSpace(token))
            Token = token.Trim();

        _repositories = repositories.ToList();
        IsEnabled = isEnabled;
        SetUpdatedAt();
    }

    /// <summary>
    /// The repository an incident in this service should be read against: the service's own
    /// mapping, else the default one, else none — in which case the analysis gets no GitHub tools
    /// at all rather than being pointed at a guess.
    /// </summary>
    public RepositoryMapping? RepositoryFor(string? service)
    {
        if (!IsEnabled)
            return null;

        if (!string.IsNullOrWhiteSpace(service))
        {
            var own = _repositories.FirstOrDefault(r =>
                !r.IsDefault && string.Equals(r.Service, service.Trim(), StringComparison.OrdinalIgnoreCase)
            );

            if (own is not null)
                return own;
        }

        return _repositories.FirstOrDefault(r => r.IsDefault);
    }
}
