using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Exceptions;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Application.Commands.RotateIngestKey;

public sealed class RotateIngestKeyCommandHandler
    : IRequestHandler<RotateIngestKeyCommand, TelemetrySourceDto>
{
    private readonly ITelemetrySourceRepository _sources;
    private readonly IRealtimeNotifier _realtime;

    public RotateIngestKeyCommandHandler(ITelemetrySourceRepository sources, IRealtimeNotifier realtime)
    {
        _sources = sources;
        _realtime = realtime;
    }

    public async Task<TelemetrySourceDto> Handle(
        RotateIngestKeyCommand request,
        CancellationToken cancellationToken
    )
    {
        // Through the organisation's own filter: another organisation's source is not found, the
        // same answer as every other read of it.
        var source =
            await _sources.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.Id);

        if (!source.Kind.IsPushed())
            throw new ValidationException(
                [
                    new ValidationFailure(
                        nameof(RotateIngestKeyCommand.Id),
                        $"A {source.Kind} source is polled; it has no ingest key to rotate."
                    ),
                ]
            );

        var issued = IngestKey.Generate();
        source.IssueIngestKey(issued.Hash, issued.DisplayPrefix);

        _sources.Update(source);
        await _sources.SaveChangesAsync(cancellationToken);

        // Broadcast without the key, returned with it — the same split as creation.
        var dto = TelemetrySourceDto.FromDomain(source);
        await _realtime.SourceChangedAsync(dto, cancellationToken);

        return dto with { IngestKey = issued.Key };
    }
}
