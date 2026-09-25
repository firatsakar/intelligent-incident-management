namespace AgentOrchestrator.Domain.ValueObjects;

/// <summary>
/// Where one service's code lives. <see cref="Service"/> is the name the telemetry reports —
/// <c>service.name</c> on a log record — or <see cref="AnyService"/> for the repository used when
/// nothing more specific matches. <see cref="Branch"/> null means the repository's default branch.
/// </summary>
public sealed record RepositoryMapping(string Service, string Owner, string Repository, string? Branch)
{
    public const string AnyService = "*";

    public bool IsDefault => Service == AnyService;

    public string FullName => $"{Owner}/{Repository}";
}
