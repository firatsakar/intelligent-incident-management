using MediatR;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Commands.UpdateIntegration;

public sealed record UpdateIntegrationCommand : IRequest<IntegrationDto>
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyDictionary<string, string> Config { get; init; }
    public IncidentPriority? MinPriority { get; init; }
    public string? CategoryFilter { get; init; }
}
