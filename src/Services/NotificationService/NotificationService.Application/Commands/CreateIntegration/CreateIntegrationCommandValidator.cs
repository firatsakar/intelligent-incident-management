using FluentValidation;
using NotificationService.Application.Validators;

namespace NotificationService.Application.Commands.CreateIntegration;

public sealed class CreateIntegrationCommandValidator : AbstractValidator<CreateIntegrationCommand>
{
    public CreateIntegrationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Channel).IsInEnum();

        RuleFor(x => x.CategoryFilter).MaximumLength(128);

        RuleFor(x => x.Config)
            .NotEmpty()
            .Custom(
                (config, context) =>
                {
                    var channel = context.InstanceToValidate.Channel;
                    var missing = IntegrationConfigRules.MissingKeys(channel, config);

                    if (missing.Count > 0)
                        context.AddFailure(
                            nameof(CreateIntegrationCommand.Config),
                            IntegrationConfigRules.Describe(channel, missing)
                        );
                }
            );
    }
}
