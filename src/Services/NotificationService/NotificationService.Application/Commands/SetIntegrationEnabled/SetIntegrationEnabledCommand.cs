using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Commands.SetIntegrationEnabled;

public sealed record SetIntegrationEnabledCommand(Guid Id, bool IsEnabled)
    : IRequest<IntegrationDto>;
