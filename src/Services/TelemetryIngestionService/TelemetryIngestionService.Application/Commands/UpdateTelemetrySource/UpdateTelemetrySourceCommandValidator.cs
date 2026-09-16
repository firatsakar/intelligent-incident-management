using FluentValidation;

namespace TelemetryIngestionService.Application.Commands.UpdateTelemetrySource;

public sealed class UpdateTelemetrySourceCommandValidator
    : AbstractValidator<UpdateTelemetrySourceCommand>
{
    public UpdateTelemetrySourceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Config).NotEmpty();

        RuleFor(x => x.PollIntervalSeconds)
            .GreaterThanOrEqualTo(5)
            .When(x => x.PollIntervalSeconds.HasValue);

        // Per-kind required keys are checked in the handler: the kind is fixed at creation and
        // only the stored source knows it.
    }
}
