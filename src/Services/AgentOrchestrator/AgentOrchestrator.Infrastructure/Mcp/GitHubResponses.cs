using System.Globalization;
using System.Text.Json;
using AgentOrchestrator.Application.Changes;

namespace AgentOrchestrator.Infrastructure.Mcp;

/// <summary>
/// Reads what GitHub's MCP tools return. The tools answer with GitHub's REST shapes serialised as
/// text — a commit list is an array of commit objects, a commit is one object with its files —
/// and every field is read defensively: a missing author or date is a thinner record, not a
/// failed analysis.
/// </summary>
/// <remarks>
/// Everything read here is written by whoever committed to the customer's repository and ends up
/// in front of the model. It is trimmed so it cannot crowd out the incident, and nothing in it is
/// ever trusted as an instruction or a link — the URLs shown to people are built by the platform.
/// </remarks>
public static class GitHubResponses
{
    public const int TitleMaxLength = 200;
    public const int MessageMaxLength = 2_000;
    public const int MaxFiles = 20;
    public const int PatchMaxLengthPerFile = 1_500;
    public const int PatchMaxLengthTotal = 6_000;

    public static IReadOnlyList<RecentChange> ParseCommits(string json)
    {
        using var document = JsonDocument.Parse(json);

        var items = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement,
            // Some tool versions wrap the list; take the first array property rather than fail.
            JsonValueKind.Object => document
                .RootElement.EnumerateObject()
                .Select(p => p.Value)
                .FirstOrDefault(v => v.ValueKind == JsonValueKind.Array),
            _ => default,
        };

        if (items.ValueKind != JsonValueKind.Array)
            throw new FormatException("The commit list was not a JSON array.");

        return items
            .EnumerateArray()
            .Select(ReadChange)
            .Where(change => change is not null)
            .Select(change => change!)
            .ToList();
    }

    public static ChangeDetail ParseCommit(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var change = ReadChange(root) ?? throw new FormatException("The commit had no sha.");
        var message = Trim(Message(root) ?? change.Title, MessageMaxLength);

        var files = new List<ChangedFile>();
        var patchBudget = PatchMaxLengthTotal;

        if (root.TryGetProperty("files", out var fileArray) && fileArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in fileArray.EnumerateArray().Take(MaxFiles))
            {
                var path = String(file, "filename") ?? String(file, "path");

                if (path is null)
                    continue;

                var patch = String(file, "patch");

                // Once the total is spent, later files are listed without their diff: the model
                // still sees what was touched, and can open nothing further to learn more.
                if (patch is not null)
                {
                    if (patchBudget < 2)
                    {
                        patch = null;
                    }
                    else
                    {
                        patch = Trim(patch, Math.Min(PatchMaxLengthPerFile, patchBudget));
                        patchBudget -= patch.Length;
                    }
                }

                files.Add(
                    new ChangedFile(
                        path,
                        String(file, "status"),
                        Int(file, "additions"),
                        Int(file, "deletions"),
                        patch
                    )
                );
            }
        }

        return new ChangeDetail(change, message, files);
    }

    private static RecentChange? ReadChange(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var sha = String(element, "sha");

        if (string.IsNullOrWhiteSpace(sha))
            return null;

        element.TryGetProperty("commit", out var commit);

        var message = Message(element) ?? string.Empty;
        var title = Trim(message.Split('\n', 2)[0].Trim(), TitleMaxLength);

        // The GitHub login when the commit is linked to an account; the name git recorded
        // otherwise. Either is only a label — nothing is looked up by it.
        var author =
            (element.TryGetProperty("author", out var account) ? String(account, "login") : null)
            ?? (commit.ValueKind == JsonValueKind.Object && commit.TryGetProperty("author", out var gitAuthor) ? String(gitAuthor, "name") : null)
            ?? (account.ValueKind == JsonValueKind.String ? account.GetString() : null);

        var date =
            Date(commit, "author")
            ?? Date(commit, "committer")
            ?? ParseDate(String(element, "date"))
            ?? DateTime.MinValue;

        return new RecentChange(sha.Trim(), title, author, date);
    }

    private static string? Message(JsonElement element) =>
        element.TryGetProperty("commit", out var commit) && commit.ValueKind == JsonValueKind.Object
            ? String(commit, "message") ?? String(element, "message")
            : String(element, "message");

    private static DateTime? Date(JsonElement commit, string who) =>
        commit.ValueKind == JsonValueKind.Object
        && commit.TryGetProperty(who, out var person)
        && person.ValueKind == JsonValueKind.Object
            ? ParseDate(String(person, "date"))
            : null;

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : 0;

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : string.Concat(value.AsSpan(0, Math.Max(0, max - 1)), "…");
}
