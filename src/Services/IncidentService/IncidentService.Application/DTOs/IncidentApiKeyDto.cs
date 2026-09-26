using IncidentService.Domain.Aggregates;

namespace IncidentService.Application.DTOs;

/// <summary>An API key as the console lists it: never the key itself, nor its hash.</summary>
public record IncidentApiKeyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string KeyPrefix { get; init; } = default!;
    public string CreatedBy { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }

    public static IncidentApiKeyDto FromDomain(IncidentApiKey key) =>
        new()
        {
            Id = key.Id,
            Name = key.Name,
            KeyPrefix = key.KeyPrefix,
            CreatedBy = key.CreatedBy,
            CreatedAt = key.CreatedAt,
            LastUsedAt = key.LastUsedAt,
        };
}

/// <summary>A key just made — the one response that carries its value.</summary>
public sealed record IssuedIncidentApiKeyDto : IncidentApiKeyDto
{
    public string Key { get; init; } = default!;

    public static IssuedIncidentApiKeyDto FromDomain(IncidentApiKey key, string secret) =>
        new()
        {
            Id = key.Id,
            Name = key.Name,
            KeyPrefix = key.KeyPrefix,
            CreatedBy = key.CreatedBy,
            CreatedAt = key.CreatedAt,
            LastUsedAt = key.LastUsedAt,
            Key = secret,
        };
}
