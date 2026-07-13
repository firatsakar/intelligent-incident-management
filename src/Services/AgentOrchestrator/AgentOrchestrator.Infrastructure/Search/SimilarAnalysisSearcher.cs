using System.Text.RegularExpressions;
using AgentOrchestrator.Application.DTOs;
using AgentOrchestrator.Domain.Enums;
using AgentOrchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Infrastructure.Search;

public sealed partial class SimilarAnalysisSearcher : ISimilarAnalysisSearcher
{
    private const int CandidatePoolSize = 25;
    private const int MinKeywordLength = 3;
    private const int MaxKeywords = 10;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the",
        "and",
        "for",
        "are",
        "was",
        "were",
        "with",
        "from",
        "that",
        "this",
        "have",
        "has",
        "not",
        "all",
        "any",
        "but",
        "its",
        "into",
        "out",
        "over",
        "under",
        "after",
        "before",
        "when",
        "then",
        "also",
        "which",
        "while",
        "being",
        "been",
        "only",
        "error",
        "errors",
        "incident",
        "service",
        "issue",
        "failed",
        "failure",
        "started",
        "began",
    };

    private readonly AgentDbContext _dbContext;
    private readonly ILogger<SimilarAnalysisSearcher> _logger;

    public SimilarAnalysisSearcher(
        AgentDbContext dbContext,
        ILogger<SimilarAnalysisSearcher> logger
    )
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SimilarAnalysis>> SearchAsync(
        string title,
        string description,
        Guid? excludeIncidentId = null,
        int maxResults = 3,
        CancellationToken cancellationToken = default
    )
    {
        var keywords = ExtractKeywords($"{title} {description}");

        if (keywords.Count == 0)
        {
            _logger.LogInformation("Similarity search skipped: no usable keywords extracted.");
            return [];
        }

        var patterns = keywords.Select(k => $"%{k}%").ToArray();

        var pool = await _dbContext
            .Analyses.AsNoTracking()
            .Where(a => a.Status == AnalysisStatus.Completed)
            .Where(a => excludeIncidentId == null || a.IncidentId != excludeIncidentId)
            .Where(a =>
                patterns.Any(p =>
                    EF.Functions.ILike(a.IncidentTitle, p)
                    || EF.Functions.ILike(a.IncidentDescription, p)
                )
            )
            .OrderByDescending(a => a.CreatedAt)
            .Take(CandidatePoolSize)
            .Select(a => new Candidate(
                a.IncidentId,
                a.IncidentTitle,
                a.IncidentDescription,
                a.Result!.SuggestedCategory,
                a.Result!.SuggestedPriority,
                a.Result!.Reasoning,
                a.Result!.SuggestedSteps
            ))
            .ToListAsync(cancellationToken);

        var ranked = pool.Select(c => new { Candidate = c, Score = ScoreMatch(c, keywords) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => new SimilarAnalysis
            {
                IncidentId = x.Candidate.IncidentId,
                Title = x.Candidate.Title,
                SuggestedCategory = x.Candidate.SuggestedCategory,
                SuggestedPriority = x.Candidate.SuggestedPriority,
                Reasoning = x.Candidate.Reasoning,
                SuggestedSteps = x.Candidate.SuggestedSteps,
                MatchScore = x.Score,
            })
            .ToList();

        _logger.LogInformation(
            "Similarity search matched {ResultCount} past analyses from a pool of {PoolSize} using {KeywordCount} keywords",
            ranked.Count,
            pool.Count,
            keywords.Count
        );

        return ranked;
    }

    private static IReadOnlyList<string> ExtractKeywords(string text)
    {
        return TokenRegex()
            .Split(text.ToLowerInvariant())
            .Where(t => t.Length >= MinKeywordLength && !StopWords.Contains(t))
            .Distinct()
            .Take(MaxKeywords)
            .ToList();
    }

    private static int ScoreMatch(Candidate candidate, IReadOnlyList<string> keywords)
    {
        var haystack = $"{candidate.Title} {candidate.Description}".ToLowerInvariant();
        return keywords.Count(k => haystack.Contains(k, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex TokenRegex();

    private sealed record Candidate(
        Guid IncidentId,
        string Title,
        string Description,
        string SuggestedCategory,
        string SuggestedPriority,
        string Reasoning,
        IReadOnlyList<string> SuggestedSteps
    );
}
