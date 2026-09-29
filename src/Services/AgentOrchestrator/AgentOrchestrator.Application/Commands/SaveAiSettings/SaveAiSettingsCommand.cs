using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Application.Queries.GetAiSettings;
using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.Enums;
using BuildingBlocks.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Application.Commands.SaveAiSettings;

/// <summary>The organisation's AI response language. Applies to analyses from now on.</summary>
/// <summary>The language by name, exactly as <see cref="AnalysisLanguages"/> spells it.</summary>
public sealed record SaveAiSettingsCommand(string? ResponseLanguage) : IRequest<AiSettingsDto>;

public sealed class SaveAiSettingsCommandValidator : AbstractValidator<SaveAiSettingsCommand>
{
    public SaveAiSettingsCommandValidator()
    {
        RuleFor(x => x.ResponseLanguage)
            .Must(value => AnalysisLanguages.TryParse(value, out _))
            .WithMessage($"Response language must be one of: {string.Join(", ", AnalysisLanguages.Names)}.");
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
        // The validator has already refused anything else; parsed again rather than trusted, so the
        // handler cannot be reached with a value it has not checked itself.
        if (!AnalysisLanguages.TryParse(request.ResponseLanguage, out var language))
            throw new ValidationException(
                [new FluentValidation.Results.ValidationFailure(nameof(request.ResponseLanguage), "Unsupported response language.")]
            );

        var settings = await _settings.GetAsync(cancellationToken);

        if (settings is null)
            await _settings.AddAsync(AiSettings.Create(_organization.Required, language), cancellationToken);
        else
            settings.ChangeLanguage(language);

        await _settings.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("AI response language set to {Language}.", language);

        return new AiSettingsDto(language);
    }
}
