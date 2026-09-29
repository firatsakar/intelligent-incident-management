using IncidentService.Domain.Aggregates;

namespace IncidentService.Application.Abstractions;

public interface IIncidentActivityRepository
{
    /// <summary>
    /// Adds a row to the same unit of work as the incident it is about:
    /// <see cref="IIncidentRepository.SaveChangesAsync"/> saves both, in one transaction.
    /// </summary>
    void Add(IncidentActivity activity);

    /// <summary>The incident's most recent <paramref name="limit"/> rows, oldest first.</summary>
    Task<IReadOnlyList<IncidentActivity>> ListForIncidentAsync(
        Guid incidentId,
        int limit,
        CancellationToken cancellationToken = default
    );
}
