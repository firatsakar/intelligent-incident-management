using FluentValidation;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Constants;

namespace IncidentService.Application.Commands.CreateIncident;

public sealed class CreateIncidentCommandValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required.")
            .MaximumLength(IncidentConstants.TitleMaxLength)
            .WithMessage($"Title must not exceed {IncidentConstants.TitleMaxLength} characters.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(IncidentConstants.DescriptionMaxLength)
            .WithMessage(
                $"Description must not exceed {IncidentConstants.DescriptionMaxLength} characters."
            );

        RuleFor(x => x.Priority).IsInEnum().WithMessage("Invalid priority value.");

        RuleFor(x => x.Source).IsInEnum().WithMessage("Invalid source value.");

        RuleFor(x => x.AssignedTeam)
            .MaximumLength(IncidentConstants.TeamMaxLength)
            .WithMessage(
                $"Assigned team must not exceed {IncidentConstants.TeamMaxLength} characters."
            )
            .When(x => x.AssignedTeam is not null);

        RuleFor(x => x.ExternalId)
            .MaximumLength(IncidentConstants.ExternalIdMaxLength)
            .WithMessage($"External id must not exceed {IncidentConstants.ExternalIdMaxLength} characters.")
            .When(x => x.ExternalId is not null);

        RuleFor(x => x.ReportedBy)
            .MaximumLength(IncidentApiKey.NameMaxLength)
            .When(x => x.ReportedBy is not null);
    }
}
