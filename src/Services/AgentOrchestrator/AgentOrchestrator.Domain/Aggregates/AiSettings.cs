using AgentOrchestrator.Domain.Enums;
using BuildingBlocks.SharedKernel;

namespace AgentOrchestrator.Domain.Aggregates;

/// <summary>
/// How the organisation's analyses are written (Adım 20.6). At most one per organisation; an
/// organisation without one gets English, as every analysis was until this existed.
/// </summary>
/// <remarks>
/// The organisation's rather than each reader's: an analysis is written once, stored, and read by
/// the whole team, so two members preferring two languages would still get one text. Only the
/// prose follows it — the reasoning and the steps. Category and priority stay the English keys
/// that notification filters match on; the console translates them for display.
/// </remarks>
public sealed class AiSettings : AggregateRoot
{
    private AiSettings() { }

    public Guid OrganizationId { get; private set; }

    public AnalysisLanguage ResponseLanguage { get; private set; }

    public static AiSettings Create(Guid organizationId, AnalysisLanguage responseLanguage) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ResponseLanguage = responseLanguage,
        };

    public void ChangeLanguage(AnalysisLanguage responseLanguage)
    {
        ResponseLanguage = responseLanguage;
        SetUpdatedAt();
    }
}
