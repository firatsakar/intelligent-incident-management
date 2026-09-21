using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetIntegrationById;

public sealed record GetIntegrationByIdQuery(Guid Id) : IRequest<IntegrationDto>;
