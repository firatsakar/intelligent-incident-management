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

    /// <summary>
    /// The five countable fields of every incident created inside the window, ordered oldest
    /// first. Shaping into buckets is the handler's job — see <see cref="IncidentStatsRow"/> for
    /// why the grouping is not pushed into SQL.
    /// </summary>
    Task<IReadOnlyList<IncidentStatsRow>> GetStatsRowsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Priority counts for everything not yet resolved or closed. <b>Deliberately ignores the
    /// window</b>: an incident opened six weeks ago and still open is the one an operator most
    /// needs to see, and a date filter would be the thing that hid it.
    /// </summary>
    Task<IReadOnlyDictionary<IncidentPriority, int>> GetOpenCountsByPriorityAsync(
        CancellationToken cancellationToken = default
    );
}
