using FluentValidation;
using TelemetryIngestionService.Application.Validators;
using TelemetryIngestionService.Domain.Enums;

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

        // A polled source cannot be reached without settings; a pushed one has none of its own.
        RuleFor(x => x.Config).NotEmpty().When(x => !x.Kind.IsPushed());

        RuleFor(x => x.Config)
            .NotNull()
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

                    if (TelemetrySourceConfigRules.Invalid(kind, config) is { } invalid)
                        context.AddFailure(nameof(CreateTelemetrySourceCommand.Config), invalid);
                }
            );
    }
}
