using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Application.Validators;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.UpdateTelemetrySource;

public sealed class UpdateTelemetrySourceCommandHandler
    : IRequestHandler<UpdateTelemetrySourceCommand, TelemetrySourceDto>
{
    private readonly ITelemetrySourceRepository _sources;

    public UpdateTelemetrySourceCommandHandler(ITelemetrySourceRepository sources)
    {
        _sources = sources;
    }

    public async Task<TelemetrySourceDto> Handle(
        UpdateTelemetrySourceCommand request,
        CancellationToken cancellationToken
    )
    {
        var source =
            await _sources.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.Id);

        // The kind is fixed at creation, so the required keys can only be resolved once the stored
        // source is in hand — which is why this check lives here and not in the validator.
        var missing = TelemetrySourceConfigRules.MissingKeys(source.Kind, request.Config);

        if (missing.Count > 0)
        {
            throw new ValidationException(
                [
                    new ValidationFailure(
                        nameof(UpdateTelemetrySourceCommand.Config),
                        TelemetrySourceConfigRules.Describe(source.Kind, missing)
                    ),
                ]
            );
        }

        source.Update(request.Name, request.PollIntervalSeconds);
        source.UpdateConfig(request.Config);

        _sources.Update(source);
        await _sources.SaveChangesAsync(cancellationToken);

        return TelemetrySourceDto.FromDomain(source);
    }
}
