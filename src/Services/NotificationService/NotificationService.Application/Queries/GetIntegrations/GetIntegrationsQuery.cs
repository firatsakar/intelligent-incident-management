using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetIntegrations;

public sealed record GetIntegrationsQuery : IRequest<IReadOnlyList<IntegrationDto>>;
