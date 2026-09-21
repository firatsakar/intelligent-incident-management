using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Queries.GetTelemetrySources;

public sealed class GetTelemetrySourcesQueryHandler
    : IRequestHandler<GetTelemetrySourcesQuery, IReadOnlyList<TelemetrySourceDto>>
{
    private readonly ITelemetrySourceRepository _sources;

    public GetTelemetrySourcesQueryHandler(ITelemetrySourceRepository sources)
    {
        _sources = sources;
    }

    public async Task<IReadOnlyList<TelemetrySourceDto>> Handle(
        GetTelemetrySourcesQuery request,
        CancellationToken cancellationToken
    )
    {
        var sources = await _sources.GetAllAsync(cancellationToken);

        return sources.Select(TelemetrySourceDto.FromDomain).ToList();
    }
}
