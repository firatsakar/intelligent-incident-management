using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Commands.TestTelemetrySource;

// Proves a source's settings reach the external system before any incident depends on them.
public sealed record TestTelemetrySourceCommand(Guid Id) : IRequest<ConnectorTestResult>;
