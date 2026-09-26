using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Enums;
using MediatR;

namespace AgentOrchestrator.Application.Queries.GetAiSettings;

public sealed record AiSettingsDto(AnalysisLanguage ResponseLanguage);

public sealed record GetAiSettingsQuery : IRequest<AiSettingsDto>;

public sealed class GetAiSettingsQueryHandler : IRequestHandler<GetAiSettingsQuery, AiSettingsDto>
{
    private readonly IAiSettingsRepository _settings;

    public GetAiSettingsQueryHandler(IAiSettingsRepository settings)
    {
        _settings = settings;
    }

    // Nothing saved yet reads as the default rather than as nothing: English, as every analysis
    // was written before the setting existed.
    public async Task<AiSettingsDto> Handle(GetAiSettingsQuery request, CancellationToken cancellationToken) =>
        new((await _settings.GetAsync(cancellationToken))?.ResponseLanguage ?? AnalysisLanguage.English);
}
