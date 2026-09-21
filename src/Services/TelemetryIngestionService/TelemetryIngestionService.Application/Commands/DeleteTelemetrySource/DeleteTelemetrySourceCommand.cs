using MediatR;

namespace TelemetryIngestionService.Application.Commands.DeleteTelemetrySource;

public sealed record DeleteTelemetrySourceCommand(Guid Id) : IRequest;
