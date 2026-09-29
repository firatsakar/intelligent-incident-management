namespace AgentOrchestrator.Domain.Enums;

/// <summary>The language an analysis writes its prose in.</summary>
public enum AnalysisLanguage
{
    English,
    Turkish,
}

/// <summary>
/// The languages an analysis can be written in, by the exact names the API accepts.
/// </summary>
/// <remarks>
/// The setting decides a line in the model's instructions, so what the API lets through is the two
/// names and nothing else: no numbers (which the JSON enum converter would otherwise map onto the
/// enum), no other casing, no surrounding text. The line itself is a fixed string per language —
/// nothing a request sends is ever copied into the prompt.
/// </remarks>
public static class AnalysisLanguages
{
    public static IReadOnlyList<string> Names { get; } =
        [nameof(AnalysisLanguage.English), nameof(AnalysisLanguage.Turkish)];

    public static bool TryParse(string? value, out AnalysisLanguage language)
    {
        switch (value)
        {
            case nameof(AnalysisLanguage.English):
                language = AnalysisLanguage.English;
                return true;
            case nameof(AnalysisLanguage.Turkish):
                language = AnalysisLanguage.Turkish;
                return true;
            default:
                language = default;
                return false;
        }
    }
}
