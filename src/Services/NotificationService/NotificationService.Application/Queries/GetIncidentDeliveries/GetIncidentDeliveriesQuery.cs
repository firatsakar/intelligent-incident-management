using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetIncidentDeliveries;

public sealed record GetIncidentDeliveriesQuery(Guid IncidentId)
    : IRequest<IReadOnlyList<NotificationDeliveryDto>>;
