using FluentValidation;
using TelemetryIngestionService.Application.Validators;

namespace TelemetryIngestionService.Application.Commands.CreateTelemetrySource;

public sealed class CreateTelemetrySourceCommandValidator
    : AbstractValidator<CreateTelemetrySourceCommand>
{
    public CreateTelemetrySourceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x.PollIntervalSeconds)
            .GreaterThanOrEqualTo(5)
            .When(x => x.PollIntervalSeconds.HasValue)
            .WithMessage("Polling faster than every 5 seconds hammers the source for no benefit.");

        RuleFor(x => x.Config)
            .NotEmpty()
            .Custom(
                (config, context) =>
                {
                    var kind = context.InstanceToValidate.Kind;
                    var missing = TelemetrySourceConfigRules.MissingKeys(kind, config);

                    if (missing.Count > 0)
                        context.AddFailure(
                            nameof(CreateTelemetrySourceCommand.Config),
                            TelemetrySourceConfigRules.Describe(kind, missing)
                        );
                }
            );
    }
}
