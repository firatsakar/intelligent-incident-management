using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Commands.SetTelemetrySourceEnabled;

public sealed record SetTelemetrySourceEnabledCommand(Guid Id, bool IsEnabled)
    : IRequest<TelemetrySourceDto>;
