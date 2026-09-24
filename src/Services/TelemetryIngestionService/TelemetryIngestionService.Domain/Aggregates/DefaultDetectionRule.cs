using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Domain.Aggregates;

/// <summary>
/// The rule an organisation starts with.
/// </summary>
/// <remarks>
/// <para>
/// Without at least one rule nothing is ever detected, and a system that silently detects nothing
/// is worse than one that is obviously misconfigured. An organisation with no rule would ingest
/// logs, build signatures and promote nothing, with no error anywhere to say so.
/// </para>
/// <para>
/// It used to be seeded once per empty table, which was right while there was one organisation and
/// wrong the moment there were two: the row belonged to nobody. It is now written when an
/// organisation announces itself, and the numbers live here so that the thing an operator will
/// later tune has a definition with a name rather than being buried in whatever wrote it.
/// </para>
/// </remarks>
public static class DefaultDetectionRule
{
    public const string Name = "Default error burst";

    /// <summary>Three errors in five minutes, deduplicated for a day, promoted at 0.90.</summary>
    public static DetectionRule For(Guid organizationId) =>
        DetectionRule.Create(
            organizationId,
            name: Name,
            // Null means every service. An organisation's first rule cannot assume which services
            // it has, because it has not read a log yet.
            service: null,
            minSeverity: LogSeverity.Error,
            windowSeconds: 300,
            threshold: 3,
            dedupWindowHours: 24,
            promoteThreshold: 0.90
        );
}
