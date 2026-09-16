using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Infrastructure.Persistence.Repositories;

public sealed class NotificationDeliveryRepository : INotificationDeliveryRepository
{
    private readonly NotificationDbContext _context;

    public NotificationDeliveryRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(
        Guid integrationId,
        Guid incidentId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.NotificationDeliveries.AnyAsync(
            x => x.IntegrationId == integrationId && x.IncidentId == incidentId,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<NotificationDelivery>> GetByIncidentIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .NotificationDeliveries.AsNoTracking()
            .Where(x => x.IncidentId == incidentId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken = default
    )
    {
        await _context.NotificationDeliveries.AddAsync(delivery, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
