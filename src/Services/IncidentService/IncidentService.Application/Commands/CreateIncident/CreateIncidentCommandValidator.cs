using FluentValidation;
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
    }
}
