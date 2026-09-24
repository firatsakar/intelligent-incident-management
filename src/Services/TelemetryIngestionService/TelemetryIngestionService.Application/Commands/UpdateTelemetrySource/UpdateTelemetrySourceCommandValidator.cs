using FluentValidation;

namespace TelemetryIngestionService.Application.Commands.UpdateTelemetrySource;

public sealed class UpdateTelemetrySourceCommandValidator
    : AbstractValidator<UpdateTelemetrySourceCommand>
{
    public UpdateTelemetrySourceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

        // Not NotEmpty: a pushed source has no settings of its own, and whether this one needs any
        // depends on a kind only the stored source knows.
        RuleFor(x => x.Config).NotNull();

        RuleFor(x => x.PollIntervalSeconds)
            .GreaterThanOrEqualTo(5)
            .When(x => x.PollIntervalSeconds.HasValue);

        // Per-kind required keys are checked in the handler: the kind is fixed at creation and
        // only the stored source knows it.
    }
}
