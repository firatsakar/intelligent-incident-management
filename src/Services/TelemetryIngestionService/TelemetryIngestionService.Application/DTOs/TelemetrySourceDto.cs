using BuildingBlocks.Application;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.DTOs;

public sealed record TelemetrySourceDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required TelemetrySourceKind Kind { get; init; }
    public required bool IsEnabled { get; init; }

    // Credential-looking values are masked: reading a source back must not hand out the customer's
    // API key. Writes still take the real values.
    public required IReadOnlyDictionary<string, string> Config { get; init; }

    public required int PollIntervalSeconds { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static TelemetrySourceDto FromDomain(TelemetrySource source)
    {
        return new TelemetrySourceDto
        {
            Id = source.Id,
            Name = source.Name,
            Kind = source.Kind,
            IsEnabled = source.IsEnabled,
            // Shared with the update handler so what is hidden on read is exactly what can be
            // restored on write.
            Config = ConfigMasking.Mask(source.Config),
            PollIntervalSeconds = source.PollIntervalSeconds,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
        };
    }
}
