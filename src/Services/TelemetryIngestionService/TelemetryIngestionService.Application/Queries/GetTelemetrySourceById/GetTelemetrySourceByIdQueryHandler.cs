using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Queries.GetTelemetrySourceById;

public sealed class GetTelemetrySourceByIdQueryHandler
    : IRequestHandler<GetTelemetrySourceByIdQuery, TelemetrySourceDto>
{
    private readonly ITelemetrySourceRepository _sources;

    public GetTelemetrySourceByIdQueryHandler(ITelemetrySourceRepository sources)
    {
        _sources = sources;
    }

    public async Task<TelemetrySourceDto> Handle(
        GetTelemetrySourceByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        var source =
            await _sources.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.Id);

        return TelemetrySourceDto.FromDomain(source);
    }
}
