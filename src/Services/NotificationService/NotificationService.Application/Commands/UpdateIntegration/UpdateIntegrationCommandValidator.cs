using FluentValidation;

namespace NotificationService.Application.Commands.UpdateIntegration;

public sealed class UpdateIntegrationCommandValidator : AbstractValidator<UpdateIntegrationCommand>
{
    public UpdateIntegrationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

        RuleFor(x => x.CategoryFilter).MaximumLength(128);

        RuleFor(x => x.Config).NotEmpty();

        // The per-channel required keys are checked in the handler, not here: the channel is
        // fixed at creation and only the stored integration knows it, and a validator has no
        // business reaching into the database.
    }
}
