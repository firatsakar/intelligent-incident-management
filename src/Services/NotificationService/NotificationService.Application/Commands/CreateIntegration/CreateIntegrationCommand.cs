using MediatR;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Commands.CreateIntegration;

public sealed record CreateIntegrationCommand : IRequest<IntegrationDto>
{
    public required string Name { get; init; }
    public required NotificationChannelType Channel { get; init; }
    public required IReadOnlyDictionary<string, string> Config { get; init; }
    public IncidentPriority? MinPriority { get; init; }
    public string? CategoryFilter { get; init; }
    public bool IsEnabled { get; init; } = true;
}
