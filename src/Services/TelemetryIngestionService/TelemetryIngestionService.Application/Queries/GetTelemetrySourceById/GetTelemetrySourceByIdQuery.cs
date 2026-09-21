using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Queries.GetTelemetrySourceById;

public sealed record GetTelemetrySourceByIdQuery(Guid Id) : IRequest<TelemetrySourceDto>;
