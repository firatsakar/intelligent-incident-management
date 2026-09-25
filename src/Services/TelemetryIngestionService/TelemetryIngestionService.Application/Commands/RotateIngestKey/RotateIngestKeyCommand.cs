using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Commands.RotateIngestKey;

// Issues a pushed source a new key and revokes the old one in the same write. The answer to a key
// that leaked, and to a key nobody wrote down.
public sealed record RotateIngestKeyCommand(Guid Id) : IRequest<TelemetrySourceDto>;
