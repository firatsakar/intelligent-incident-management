namespace AgentOrchestrator.Application.Changes;

/// <summary>One commit, as the analysis is shown it: enough to judge whether it is worth opening.</summary>
public sealed record RecentChange(string Sha, string Title, string? Author, DateTime CommittedAt)
{
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
}

/// <summary>What one file in a commit did. <see cref="Patch"/> is trimmed, never the whole diff.</summary>
public sealed record ChangedFile(string Path, string? Status, int Additions, int Deletions, string? Patch);

/// <summary>A commit opened up: its full message and the files it touched.</summary>
public sealed record ChangeDetail(RecentChange Change, string Message, IReadOnlyList<ChangedFile> Files);
