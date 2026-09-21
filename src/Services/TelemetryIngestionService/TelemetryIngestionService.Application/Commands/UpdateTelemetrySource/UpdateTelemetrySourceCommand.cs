using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Commands.UpdateTelemetrySource;

public sealed record UpdateTelemetrySourceCommand : IRequest<TelemetrySourceDto>
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyDictionary<string, string> Config { get; init; }
    public int? PollIntervalSeconds { get; init; }
}
