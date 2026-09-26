using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Queries.GetAiSettings;
using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.Enums;
using BuildingBlocks.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Application.Commands.SaveAiSettings;

/// <summary>The organisation's AI response language (Adım 20.6). Applies to analyses from now on.</summary>
public sealed record SaveAiSettingsCommand(AnalysisLanguage ResponseLanguage) : IRequest<AiSettingsDto>;

public sealed class SaveAiSettingsCommandValidator : AbstractValidator<SaveAiSettingsCommand>
{
    public SaveAiSettingsCommandValidator()
    {
        RuleFor(x => x.ResponseLanguage).IsInEnum();
    }
}

public sealed class SaveAiSettingsCommandHandler : IRequestHandler<SaveAiSettingsCommand, AiSettingsDto>
{
    private readonly IAiSettingsRepository _settings;
    private readonly IOrganizationContext _organization;
    private readonly ILogger<SaveAiSettingsCommandHandler> _logger;

    public SaveAiSettingsCommandHandler(
        IAiSettingsRepository settings,
        IOrganizationContext organization,
        ILogger<SaveAiSettingsCommandHandler> logger
    )
    {
        _settings = settings;
        _organization = organization;
        _logger = logger;
    }

    public async Task<AiSettingsDto> Handle(SaveAiSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);

        if (settings is null)
            await _settings.AddAsync(AiSettings.Create(_organization.Required, request.ResponseLanguage), cancellationToken);
        else
            settings.ChangeLanguage(request.ResponseLanguage);

        await _settings.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("AI response language set to {Language}.", request.ResponseLanguage);

        return new AiSettingsDto(request.ResponseLanguage);
    }
}
