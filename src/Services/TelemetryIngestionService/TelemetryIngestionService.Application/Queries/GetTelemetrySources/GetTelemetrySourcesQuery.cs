using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Queries.GetTelemetrySources;

public sealed record GetTelemetrySourcesQuery : IRequest<IReadOnlyList<TelemetrySourceDto>>;
