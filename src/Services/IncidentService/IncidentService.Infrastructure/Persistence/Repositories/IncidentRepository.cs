using IncidentService.Application.Abstractions;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IncidentService.Infrastructure.Persistence.Repositories;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly IncidentDbContext _context;

    public IncidentRepository(IncidentDbContext context)
    {
        _context = context;
    }

    public async Task<Incident?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Incidents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        await _context.Incidents.AddAsync(incident, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Incident> Items, int TotalCount)> GetPagedAsync(
        IncidentStatus? status,
        IncidentPriority? priority,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Incidents.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(x => x.Priority == priority.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Update(Incident incident)
    {
        _context.Incidents.Update(incident);
    }
}
