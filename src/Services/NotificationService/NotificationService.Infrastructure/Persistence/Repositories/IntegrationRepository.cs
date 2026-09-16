using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Infrastructure.Persistence.Repositories;

public sealed class IntegrationRepository : IIntegrationRepository
{
    private readonly NotificationDbContext _context;

    public IntegrationRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<Integration?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Integrations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Integration>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Integrations.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Integration>> GetEnabledAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Integrations.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Integration integration,
        CancellationToken cancellationToken = default
    )
    {
        await _context.Integrations.AddAsync(integration, cancellationToken);
    }

    public void Update(Integration integration)
    {
        _context.Integrations.Update(integration);
    }

    public void Remove(Integration integration)
    {
        _context.Integrations.Remove(integration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
