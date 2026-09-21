using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetIncidentDeliveries;

public sealed class GetIncidentDeliveriesQueryHandler
    : IRequestHandler<GetIncidentDeliveriesQuery, IReadOnlyList<NotificationDeliveryDto>>
{
    private readonly INotificationDeliveryRepository _deliveries;

    public GetIncidentDeliveriesQueryHandler(INotificationDeliveryRepository deliveries)
    {
        _deliveries = deliveries;
    }

    public async Task<IReadOnlyList<NotificationDeliveryDto>> Handle(
        GetIncidentDeliveriesQuery request,
        CancellationToken cancellationToken
    )
    {
        var deliveries = await _deliveries.GetByIncidentIdAsync(
            request.IncidentId,
            cancellationToken
        );

        return deliveries.Select(NotificationDeliveryDto.FromDomain).ToList();
    }
}
