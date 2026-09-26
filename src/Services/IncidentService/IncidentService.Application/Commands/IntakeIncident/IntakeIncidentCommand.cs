using FluentValidation;
using IncidentService.Domain.Constants;
using IncidentService.Domain.Enums;
using MediatR;

namespace IncidentService.Application.Commands.IntakeIncident;

/// <summary>
/// An incident sent by an external system with an organisation's API key (Adım 27).
/// </summary>
/// <remarks>
/// Only what a sender can know: no team, no source (it is <see cref="IncidentSource.Alert"/> by
/// definition), and a priority that is optional — the analysis suggests one either way.
/// </remarks>
public sealed record IntakeIncidentCommand : IRequest<IntakeIncidentResult>
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public IncidentPriority? Priority { get; init; }
    public string? ExternalId { get; init; }
    public DateTime? DetectedAt { get; init; }

    /// <summary>The name of the key it came with. From the key, never from the body.</summary>
    public required string KeyName { get; init; }
}

/// <summary>
/// All a key learns back: which incident its request is about, and whether it opened it. Nothing
/// of what the organisation already had — a leaked key must not read incidents.
/// </summary>
public sealed record IntakeIncidentResult(Guid Id, bool Created);

public sealed class IntakeIncidentCommandValidator : AbstractValidator<IntakeIncidentCommand>
{
    public IntakeIncidentCommandValidator()
    {
        // The same limits as an incident opened in the console, checked here so a sender is told
        // in its own field names.
        RuleFor(x => x.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title is required.")
            .MaximumLength(IncidentConstants.TitleMaxLength)
            .WithMessage($"Title must not exceed {IncidentConstants.TitleMaxLength} characters.");

        RuleFor(x => x.Description)
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .WithMessage("Description is required.")
            .MaximumLength(IncidentConstants.DescriptionMaxLength)
            .WithMessage($"Description must not exceed {IncidentConstants.DescriptionMaxLength} characters.");

        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority is not null).WithMessage("Invalid priority value.");

        RuleFor(x => x.ExternalId)
            .MaximumLength(IncidentConstants.ExternalIdMaxLength)
            .WithMessage($"External id must not exceed {IncidentConstants.ExternalIdMaxLength} characters.")
            .When(x => x.ExternalId is not null);
    }
}
