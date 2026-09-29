using IncidentService.Application.Abstractions;
using IncidentService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IncidentService.Infrastructure.Persistence.Repositories;

public sealed class IncidentActivityRepository : IIncidentActivityRepository
{
    private readonly IncidentDbContext _context;

    public IncidentActivityRepository(IncidentDbContext context)
    {
        _context = context;
    }

    public void Add(IncidentActivity activity) => _context.IncidentActivities.Add(activity);

    public async Task<IReadOnlyList<IncidentActivity>> ListForIncidentAsync(
        Guid incidentId,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var newest = await _context
            .IncidentActivities.AsNoTracking()
            .Where(x => x.IncidentId == incidentId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        newest.Reverse();

        return newest;
    }
}
