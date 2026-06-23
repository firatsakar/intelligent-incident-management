using IncidentService.Application.Abstractions;
using IncidentService.Domain.Aggregates;
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
        Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Incidents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        Incident incident, CancellationToken cancellationToken = default)
    {
        await _context.Incidents.AddAsync(incident, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}