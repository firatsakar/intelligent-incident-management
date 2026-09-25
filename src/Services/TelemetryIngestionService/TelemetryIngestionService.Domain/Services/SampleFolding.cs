namespace TelemetryIngestionService.Domain.Services;

/// <summary>
/// Decides, for one batch, which events are stored as rows and how many each stored row stands
/// for.
/// </summary>
/// <remarks>
/// <para>
/// The decision recorded for Adım 13.5: a signature's count and a few sample lines are kept, not
/// every line. Three hundred copies of one error are one fact and a number; the ids that differ
/// between them are exactly what the fingerprint already masks.
/// </para>
/// <para>
/// Per signature, the earliest <see cref="SamplesPerSignature"/> − 1 events are kept as they are
/// and the newest is kept carrying everything between. Four rules make that safe:
/// </para>
/// <list type="bullet">
/// <item>Fatal is never folded. One crash is conclusive on its own, and each is its own evidence.</item>
/// <item>Records without a fingerprint — below Error — are never folded; they are context, and
/// their volume is the source's filter to control.</item>
/// <item>The newest event of a signature is always stored, so the counts land at the end of the
/// burst rather than before it, where a window measured from now would miss them.</item>
/// <item>Nothing at the batch's newest timestamp is folded. That instant is what a polling cursor
/// reads again next time — deliberately, to leave no gap — and a re-read event is only recognised
/// as a duplicate if its row exists.</item>
/// </list>
/// </remarks>
public static class SampleFolding
{
    public const int SamplesPerSignature = 10;

    public readonly record struct Candidate(string? Fingerprint, DateTime Timestamp, bool IsFatal);

    /// <summary>
    /// One weight per candidate, in order: 0 for an event counted onto another row instead of
    /// stored, otherwise how many events the stored row stands for. The weights always sum to the
    /// number of candidates.
    /// </summary>
    public static int[] Weigh(IReadOnlyList<Candidate> candidates)
    {
        var weights = new int[candidates.Count];
        Array.Fill(weights, 1);

        if (candidates.Count == 0)
            return weights;

        var batchNewest = candidates.Max(candidate => candidate.Timestamp);

        var groups = Enumerable
            .Range(0, candidates.Count)
            .Where(index => candidates[index].Fingerprint is not null && !candidates[index].IsFatal)
            .GroupBy(index => candidates[index].Fingerprint!, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            // Oldest first, ties in arrival order, so the same batch always folds the same way.
            var ordered = group
                .OrderBy(index => candidates[index].Timestamp)
                .ThenBy(index => index)
                .ToList();

            if (ordered.Count <= SamplesPerSignature)
                continue;

            var carrier = ordered[^1];

            foreach (var index in ordered.Skip(SamplesPerSignature - 1).SkipLast(1))
            {
                if (candidates[index].Timestamp == batchNewest)
                    continue;

                weights[index] = 0;
                weights[carrier]++;
            }
        }

        return weights;
    }
}
