using AgentOrchestrator.Infrastructure.Mcp;

namespace AgentOrchestrator.Tests;

// What GitHub's MCP tools return, read into what the analysis is shown. The shapes are GitHub's
// REST ones (list_commits → array of commits, get_commit → one commit with files), confirmed
// against the live server in Adım 17.5 Parça 3.
public sealed class GitHubResponsesTests
{
    private const string CommitList = """
        [
          {
            "sha": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
            "commit": {
              "message": "Reduce payment gateway timeout to 500ms\n\nThe gateway is usually fast.",
              "author": { "name": "Ayşe Yılmaz", "date": "2026-09-25T14:05:00Z" }
            },
            "author": { "login": "ayse" },
            "html_url": "https://github.com/acme/shop/commit/a1b2c3d"
          },
          {
            "sha": "0f9e8d7c6b5a4938271605f4e3d2c1b0a9f8e7d6",
            "commit": {
              "message": "Bump Npgsql",
              "author": { "name": "Deploy Bot", "date": "2026-09-24T09:00:00Z" }
            },
            "author": null
          },
          { "commit": { "message": "no sha, skipped" } }
        ]
        """;

    [Fact]
    public void AListReadsTitleAuthorAndDateAndSkipsWhatHasNoSha()
    {
        var changes = GitHubResponses.ParseCommits(CommitList);

        Assert.Equal(2, changes.Count);

        var first = changes[0];
        Assert.Equal("a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", first.Sha);
        Assert.Equal("a1b2c3d", first.ShortSha);
        Assert.Equal("Reduce payment gateway timeout to 500ms", first.Title);
        Assert.Equal("ayse", first.Author);
        Assert.Equal(new DateTime(2026, 9, 25, 14, 5, 0, DateTimeKind.Utc), first.CommittedAt);

        // No linked account: the name git recorded.
        Assert.Equal("Deploy Bot", changes[1].Author);
    }

    [Fact]
    public void AWrappedListIsReadToo()
    {
        var changes = GitHubResponses.ParseCommits($$"""{ "commits": {{CommitList}} }""");

        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void AnythingButAListIsAFormatError()
    {
        Assert.Throws<FormatException>(() => GitHubResponses.ParseCommits("\"Bad credentials\""));
    }

    [Fact]
    public void ACommitIsReadWithItsFilesAndTrimmedPatches()
    {
        var longPatch = new string('+', 5_000);
        var files = string.Join(
            ",",
            Enumerable.Range(1, 30).Select(i => $$"""{ "filename": "src/File{{i}}.cs", "status": "modified", "additions": {{i}}, "deletions": 1, "patch": "{{longPatch}}" }""")
        );

        var detail = GitHubResponses.ParseCommit(
            $$"""
            {
              "sha": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
              "commit": { "message": "Reduce payment gateway timeout to 500ms\n\nThe gateway is usually fast.", "author": { "name": "Ayşe", "date": "2026-09-25T14:05:00Z" } },
              "author": { "login": "ayse" },
              "files": [ {{files}} ]
            }
            """
        );

        Assert.Equal("Reduce payment gateway timeout to 500ms", detail.Change.Title);
        Assert.Contains("usually fast", detail.Message);

        Assert.Equal(GitHubResponses.MaxFiles, detail.Files.Count);
        Assert.Equal("src/File1.cs", detail.Files[0].Path);
        Assert.Equal(1, detail.Files[0].Additions);

        // Per file and in total: the diff must not crowd the incident out of the model's context.
        Assert.All(detail.Files.Where(f => f.Patch is not null), f => Assert.True(f.Patch!.Length <= GitHubResponses.PatchMaxLengthPerFile));
        Assert.True(detail.Files.Sum(f => f.Patch?.Length ?? 0) <= GitHubResponses.PatchMaxLengthTotal);
    }

    [Fact]
    public void ALongTitleIsTrimmed()
    {
        var title = new string('x', 500);
        var change = GitHubResponses.ParseCommits($$"""[ { "sha": "abc1234", "commit": { "message": "{{title}}" } } ]""")[0];

        Assert.True(change.Title.Length <= GitHubResponses.TitleMaxLength);
        Assert.Equal(DateTime.MinValue, change.CommittedAt);
    }
}
