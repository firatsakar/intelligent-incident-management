using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Commands.PollTelemetrySource;

public sealed record PollTelemetrySourceCommand(Guid SourceId) : IRequest<PollResult>;
