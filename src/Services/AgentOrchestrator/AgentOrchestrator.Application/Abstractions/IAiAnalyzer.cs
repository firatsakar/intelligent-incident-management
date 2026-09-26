using AgentOrchestrator.Application.Changes;
using AgentOrchestrator.Domain.Enums;
using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Application.Abstractions;

public interface IAiAnalyzer
{
    /// <param name="code">
    /// The organisation's code for the failing service (Adım 17.5), or null when there is none to
    /// read — no connection, no mapping, or no service named. Null is the normal case.
    /// </param>
    /// <param name="organizationId">
    /// Whose incident this is. Never shown to the model: it goes into the search tool's closure,
    /// alongside the incident id the tool already excludes, so the model can search its own
    /// hypothesis but cannot widen the search past the organisation it is working for.
    /// </param>
    Task<AnalysisResult> AnalyzeAsync(
        Guid organizationId,
        Guid incidentId,
        string title,
        string description,
        CodeContext? code,
        // The organisation's response language (Adım 20.6): the reasoning and the steps are
        // written in it; category and priority stay English keys.
        AnalysisLanguage language,
        CancellationToken cancellationToken = default
    );
}
