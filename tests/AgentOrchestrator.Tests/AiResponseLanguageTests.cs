using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Changes;
using AgentOrchestrator.Application.Commands.AnalyzeIncident;
using AgentOrchestrator.Application.Commands.SaveAiSettings;
using AgentOrchestrator.Application.Queries.GetAiSettings;
using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.Enums;
using AgentOrchestrator.Domain.ValueObjects;
using AgentOrchestrator.Infrastructure.Ai;
using BuildingBlocks.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgentOrchestrator.Tests;

// Adım 20.6: an organisation reads its analyses in its own language. The setting is the
// organisation's, English until it chooses; only the prose follows it.
public sealed class AiResponseLanguageTests
{
    private static readonly Guid Organization = Guid.NewGuid();

    private readonly IAiSettingsRepository _settings = Substitute.For<IAiSettingsRepository>();
    private readonly OrganizationContext _organization = new();

    public AiResponseLanguageTests() => _organization.Set(Organization);

    [Fact]
    public async Task AnOrganisationThatHasNotChosenReadsEnglish() =>
        Assert.Equal(AnalysisLanguage.English, (await new GetAiSettingsQueryHandler(_settings).Handle(new GetAiSettingsQuery(), default)).ResponseLanguage);

    [Fact]
    public async Task TheFirstSaveCreatesTheOrganisationsSettingsAndLaterOnesChangeThem()
    {
        AiSettings? added = null;
        await _settings.AddAsync(Arg.Do<AiSettings>(s => added = s), Arg.Any<CancellationToken>());

        var handler = new SaveAiSettingsCommandHandler(_settings, _organization, NullLogger<SaveAiSettingsCommandHandler>.Instance);

        await handler.Handle(new SaveAiSettingsCommand(AnalysisLanguage.Turkish), default);

        Assert.NotNull(added);
        Assert.Equal(Organization, added.OrganizationId);
        Assert.Equal(AnalysisLanguage.Turkish, added.ResponseLanguage);

        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(added);
        await handler.Handle(new SaveAiSettingsCommand(AnalysisLanguage.English), default);

        Assert.Equal(AnalysisLanguage.English, added.ResponseLanguage);
        await _settings.Received(1).AddAsync(Arg.Any<AiSettings>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, AnalysisLanguage.English)]
    [InlineData(AnalysisLanguage.Turkish, AnalysisLanguage.Turkish)]
    public async Task TheAnalysisIsAskedForInTheOrganisationsLanguage(AnalysisLanguage? chosen, AnalysisLanguage expected)
    {
        if (chosen is { } language)
            _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(AiSettings.Create(Organization, language));

        var analyzer = Substitute.For<IAiAnalyzer>();
        AnalysisLanguage? asked = null;
        analyzer
            .AnalyzeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CodeContext?>(), Arg.Do<AnalysisLanguage>(l => asked = l), Arg.Any<CancellationToken>())
            .Returns(new AnalysisResult { SuggestedPriority = "High", SuggestedCategory = "Application", Reasoning = "r" });

        var handler = new AnalyzeIncidentCommandHandler(
            analyzer,
            Substitute.For<IIncidentAnalysisRepository>(),
            NullLogger<AnalyzeIncidentCommandHandler>.Instance,
            _organization,
            Substitute.For<IGitHubConnectionRepository>(),
            _settings
        );

        await handler.Handle(new AnalyzeIncidentCommand { IncidentId = Guid.NewGuid(), Title = "t", Description = "d" }, default);

        Assert.Equal(expected, asked);
    }

    [Fact]
    public void TurkishAsksForTurkishProseAndKeepsTheKeysAndSearchesEnglish()
    {
        var turkish = MafAiAnalyzer.BuildInstructions(repository: null, AnalysisLanguage.Turkish);
        var english = MafAiAnalyzer.BuildInstructions(repository: null, AnalysisLanguage.English);

        Assert.Contains("RESPONSE LANGUAGE", turkish);
        Assert.Contains("in Turkish", turkish);
        Assert.Contains("\"suggestedCategory\" exactly as one of the English values", turkish);
        Assert.Contains("search_similar_incidents queries in English", turkish);
        Assert.DoesNotContain("RESPONSE LANGUAGE", english);
    }
}
