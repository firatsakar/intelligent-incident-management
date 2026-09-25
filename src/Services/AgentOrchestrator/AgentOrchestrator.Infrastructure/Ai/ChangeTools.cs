using System.ComponentModel;
using System.Text.Json;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Changes;
using AgentOrchestrator.Domain.ValueObjects;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Infrastructure.Ai;

/// <summary>
/// The two tools an analysis gets when its service's code is mapped (Adım 17.5), and the memory of
/// what they returned.
/// </summary>
/// <remarks>
/// <para>
/// Intent, not data shape: <c>list_recent_changes</c> takes no arguments at all. The repository,
/// the branch and the window are fixed here, in the closure, as <c>search_similar_incidents</c>
/// fixes the organisation — the model can decide to look, but not where.
/// </para>
/// <para>
/// <c>inspect_change</c> opens only what <c>list_recent_changes</c> returned in this analysis, and
/// <see cref="Resolve"/> accepts only those shas as the analysis's related changes. A sha the model
/// invented, or copied out of a commit message, is dropped rather than shown as a link.
/// </para>
/// </remarks>
internal sealed class ChangeTools
{
    public static readonly TimeSpan Lookback = TimeSpan.FromHours(48);

    // Commits pushed a few minutes after the first error can still be the cause: deploys land
    // after the merge, and the source's clock is not ours.
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    public const int MaxChanges = 20;
    public const int MaxListCalls = 2;
    public const int MaxInspections = 5;

    private readonly IRepositoryChangeSource _source;
    private readonly RepositoryMapping _repository;
    private readonly DateTime _problemStartedAt;
    private readonly ILogger _logger;

    // Keyed by full and short sha, so the model may name either.
    private readonly Dictionary<string, RecentChange> _seen = new(StringComparer.OrdinalIgnoreCase);

    private int _listCalls;
    private int _inspections;

    public ChangeTools(
        IRepositoryChangeSource source,
        RepositoryMapping repository,
        DateTime problemStartedAt,
        ILogger logger
    )
    {
        _source = source;
        _repository = repository;
        _problemStartedAt = problemStartedAt;
        _logger = logger;
    }

    public RepositoryMapping Repository => _repository;

    public IReadOnlyList<AIFunction> Functions =>
        [
            AIFunctionFactory.Create(
                ListRecentChangesAsync,
                name: "list_recent_changes",
                description: "Lists the commits made to this service's code in the 48 hours before the problem started "
                    + "(and a few minutes after), newest first, with how many minutes before the problem each landed. "
                    + "Use it once you have a hypothesis, to see whether a recent change could explain it."
            ),
            AIFunctionFactory.Create(
                InspectChangeAsync,
                name: "inspect_change",
                description: "Shows one commit returned by list_recent_changes: its full message and a trimmed diff of the "
                    + "files it touched. Only open commits whose title or timing makes them plausible causes."
            ),
        ];

    public async Task<string> ListRecentChangesAsync(CancellationToken cancellationToken = default)
    {
        if (++_listCalls > MaxListCalls)
            return "The recent changes were already listed above; use those.";

        try
        {
            var changes = await _source.ListChangesAsync(
                _repository,
                _problemStartedAt - Lookback,
                _problemStartedAt + Grace,
                MaxChanges,
                cancellationToken
            );

            foreach (var change in changes)
            {
                _seen[change.Sha] = change;
                _seen[change.ShortSha] = change;
            }

            _logger.LogInformation(
                "AI listed recent changes in {Repository}: {Count} in the window.",
                _repository.FullName,
                changes.Count
            );

            if (changes.Count == 0)
                return "No commits were made to this service's code in the 48 hours before the problem started.";

            return JsonSerializer.Serialize(
                changes.Select(change => new
                {
                    sha = change.ShortSha,
                    title = change.Title,
                    author = change.Author,
                    committedAt = change.CommittedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"),
                    minutesBeforeProblem = (int)Math.Round((_problemStartedAt - change.CommittedAt).TotalMinutes),
                })
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Unavailable(ex);
        }
    }

    public async Task<string> InspectChangeAsync(
        [Description("The sha of a commit returned by list_recent_changes.")] string sha,
        CancellationToken cancellationToken = default
    )
    {
        if (!_seen.TryGetValue(sha.Trim(), out var listed))
            return "Only commits returned by list_recent_changes can be inspected. Call it first and use one of its shas.";

        if (++_inspections > MaxInspections)
            return "Enough commits have been inspected for this incident; decide with what you have.";

        try
        {
            var detail = await _source.GetChangeAsync(_repository, listed.Sha, cancellationToken);

            if (detail is null)
                return $"GitHub has no commit {listed.ShortSha} in this repository.";

            _logger.LogInformation(
                "AI inspected {Sha} in {Repository}: {Files} file(s), {WithDiff} with a diff.",
                listed.ShortSha,
                _repository.FullName,
                detail.Files.Count,
                detail.Files.Count(file => file.Patch is not null)
            );

            return JsonSerializer.Serialize(
                new
                {
                    sha = detail.Change.ShortSha,
                    title = detail.Change.Title,
                    message = detail.Message,
                    files = detail.Files.Select(file => new
                    {
                        path = file.Path,
                        status = file.Status,
                        additions = file.Additions,
                        deletions = file.Deletions,
                        patch = file.Patch,
                    }),
                }
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Unavailable(ex);
        }
    }

    /// <summary>
    /// The model's related changes, kept only where this analysis actually saw the commit, and
    /// turned into records whose every field — the URL included — the platform wrote.
    /// </summary>
    public IReadOnlyList<RelatedChange> Resolve(IEnumerable<string>? shas)
    {
        if (shas is null)
            return [];

        var resolved = new List<RelatedChange>();

        foreach (var sha in shas)
        {
            if (string.IsNullOrWhiteSpace(sha) || !_seen.TryGetValue(sha.Trim(), out var change))
            {
                _logger.LogWarning("The analysis named a change it was never shown ({Sha}); dropped.", sha);
                continue;
            }

            if (resolved.Any(r => r.Sha == change.Sha))
                continue;

            resolved.Add(
                new RelatedChange
                {
                    Sha = change.Sha,
                    Title = change.Title,
                    Author = change.Author,
                    CommittedAt = change.CommittedAt,
                    Url = $"https://github.com/{Uri.EscapeDataString(_repository.Owner)}/{Uri.EscapeDataString(_repository.Repository)}/commit/{change.Sha}",
                }
            );
        }

        return resolved;
    }

    // The analysis goes on without GitHub: it is extra evidence, never a precondition.
    private string Unavailable(Exception ex)
    {
        _logger.LogWarning(ex, "GitHub could not be read for {Repository} during an analysis.", _repository.FullName);

        return "GitHub is unavailable right now, so recent changes cannot be checked. Continue the analysis without them.";
    }
}
