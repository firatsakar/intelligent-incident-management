using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Commands.CreateTelemetrySource;

public sealed class CreateTelemetrySourceCommandHandler
    : IRequestHandler<CreateTelemetrySourceCommand, TelemetrySourceDto>
{
    private readonly ITelemetrySourceRepository _sources;

    public CreateTelemetrySourceCommandHandler(ITelemetrySourceRepository sources)
    {
        _sources = sources;
    }

    public async Task<TelemetrySourceDto> Handle(
        CreateTelemetrySourceCommand request,
        CancellationToken cancellationToken
    )
    {
        var source = TelemetrySource.Create(
            request.Name,
            request.Kind,
            request.Config,
            request.PollIntervalSeconds,
            request.IsEnabled
        );

        await _sources.AddAsync(source, cancellationToken);
        await _sources.SaveChangesAsync(cancellationToken);

        return TelemetrySourceDto.FromDomain(source);
    }
}
