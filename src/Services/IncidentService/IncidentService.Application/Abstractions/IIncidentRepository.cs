using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;

namespace IncidentService.Application.Abstractions;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Incident incident, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // Yeni metodlar
    Task<(IReadOnlyList<Incident> Items, int TotalCount)> GetPagedAsync(
        IncidentStatus? status,
        IncidentPriority? priority,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    void Update(Incident incident);
}
