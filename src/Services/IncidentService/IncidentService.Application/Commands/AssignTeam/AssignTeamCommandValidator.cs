using FluentValidation;
using IncidentService.Domain.Constants;

namespace IncidentService.Application.Commands.AssignTeam;

public sealed class AssignTeamCommandValidator : AbstractValidator<AssignTeamCommand>
{
    public AssignTeamCommandValidator()
    {
        RuleFor(x => x.Team)
            .NotEmpty()
            .WithMessage("Team is required.")
            .MaximumLength(IncidentConstants.TeamMaxLength)
            .WithMessage($"Team must not exceed {IncidentConstants.TeamMaxLength} characters.");
    }
}
