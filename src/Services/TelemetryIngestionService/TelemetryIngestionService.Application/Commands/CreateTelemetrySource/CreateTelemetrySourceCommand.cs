using MediatR;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Commands.CreateTelemetrySource;

public sealed record CreateTelemetrySourceCommand : IRequest<TelemetrySourceDto>
{
    public required string Name { get; init; }
    public required TelemetrySourceKind Kind { get; init; }
    public required IReadOnlyDictionary<string, string> Config { get; init; }
    public int? PollIntervalSeconds { get; init; }
    public bool IsEnabled { get; init; } = true;
}
