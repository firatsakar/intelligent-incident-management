using BuildingBlocks.Application;
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
    private readonly IRealtimeNotifier _realtime;

    public UpdateTelemetrySourceCommandHandler(
        ITelemetrySourceRepository sources,
        IRealtimeNotifier realtime
    )
    {
        _sources = sources;
        _realtime = realtime;
    }

    public async Task<TelemetrySourceDto> Handle(
        UpdateTelemetrySourceCommand request,
        CancellationToken cancellationToken
    )
    {
        var source =
            await _sources.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.Id);

        // A read masks the API key, so that is the only value an edit form can send back for it.
        // Restoring it here is what stops a save from replacing the customer's key with "***",
        // which would break polling silently — the source would simply stop returning events.
        //
        // Done before validation, so a restored secret counts as present.
        var config = ConfigMasking.Restore(request.Config, source.Config);

        // The kind is fixed at creation, so the required keys can only be resolved once the stored
        // source is in hand — which is why this check lives here and not in the validator.
        var missing = TelemetrySourceConfigRules.MissingKeys(source.Kind, config);

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

        if (TelemetrySourceConfigRules.Invalid(source.Kind, config) is { } invalid)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(UpdateTelemetrySourceCommand.Config), invalid)]
            );
        }

        source.Update(request.Name, request.PollIntervalSeconds);
        source.UpdateConfig(config);

        _sources.Update(source);
        await _sources.SaveChangesAsync(cancellationToken);

        // After the save, never before: what is broadcast has to be what is stored. The DTO
        // masks the credentials, so this carries exactly what a GET would.
        var dto = TelemetrySourceDto.FromDomain(source);
        await _realtime.SourceChangedAsync(dto, cancellationToken);

        return dto;
    }
}
