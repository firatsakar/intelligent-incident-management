using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Abstractions;

public interface ITelemetrySourceRepository
{
    Task<TelemetrySource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetrySource>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetrySource>> GetEnabledAsync(
        CancellationToken cancellationToken = default
    );
    Task AddAsync(TelemetrySource source, CancellationToken cancellationToken = default);
    void Update(TelemetrySource source);
    void Remove(TelemetrySource source);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
