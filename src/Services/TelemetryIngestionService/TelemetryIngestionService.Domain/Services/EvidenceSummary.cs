using System.Globalization;
using System.Text;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Domain.Services;

// The incident description a promoted signal carries. Deliberately compact and computed here
// rather than by shipping raw logs to a model: the AI analysis that follows already reads the
// description, so this costs no extra call and no extra tokens beyond a few hundred — while
// giving that analysis the explicit technical evidence its confidence calibration asks for.
public static class EvidenceSummary
{
    private const int MaxStackTraceLength = 1200;

    public static string Build(
        ErrorSignature signature,
        Signal signal,
        int distinctServices,
        string? sampleStackTrace
    )
    {
        var summary = new StringBuilder();

        summary.AppendLine(
            $"Detected automatically from telemetry: {signature.Service} raised this error "
                + $"{signal.OccurrenceCount} time(s) between {signal.WindowStart:u} and {signal.WindowEnd:u}."
        );
        summary.AppendLine();

        summary.AppendLine("Evidence");
        summary.AppendLine($"- Exception: {signature.ExceptionType ?? "none reported"}");
        summary.AppendLine($"- Message: {signature.NormalizedMessage}");
        summary.AppendLine($"- First seen: {signature.FirstSeenAt:u}");
        summary.AppendLine($"- Last seen: {signature.LastSeenAt:u}");
        summary.AppendLine($"- Occurrences in window: {signal.OccurrenceCount}");
        summary.AppendLine($"- Total occurrences recorded: {signature.OccurrenceCount}");
        summary.AppendLine($"- Affected services: {distinctServices}");
        summary.AppendLine(
            $"- Detection confidence: {signal.Confidence.ToString("P0", CultureInfo.InvariantCulture)}"
        );

        if (signature.PromotionCount > 0)
        {
            summary.AppendLine(
                $"- Prior history: promoted {signature.PromotionCount} time(s), "
                    + $"{signature.ConfirmedRealCount} confirmed real, {signature.FalsePositiveCount} false positive"
            );
        }

        if (signal.ScoreBreakdown.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine("Why this was raised");

            foreach (var (component, value) in signal.ScoreBreakdown.OrderByDescending(x => x.Value))
            {
                summary.AppendLine(
                    $"- {component}: {value.ToString("F2", CultureInfo.InvariantCulture)}"
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(sampleStackTrace))
        {
            summary.AppendLine();
            summary.AppendLine("Representative stack trace");
            summary.AppendLine(Truncate(sampleStackTrace));
        }

        return summary.ToString();
    }

    public static string BuildTitle(ErrorSignature signature)
    {
        var subject = signature.ExceptionType ?? signature.NormalizedMessage;

        // Namespaces make a poor headline; the type name carries the meaning.
        var shortName = subject.Contains('.') ? subject[(subject.LastIndexOf('.') + 1)..] : subject;

        return $"{signature.Service}: {shortName}";
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaxStackTraceLength ? value : value[..MaxStackTraceLength] + "…";
    }
}
