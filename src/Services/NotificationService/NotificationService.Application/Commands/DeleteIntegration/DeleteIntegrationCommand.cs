using MediatR;

namespace NotificationService.Application.Commands.DeleteIntegration;

public sealed record DeleteIntegrationCommand(Guid Id) : IRequest;
