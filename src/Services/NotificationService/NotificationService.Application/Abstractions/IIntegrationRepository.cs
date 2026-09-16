using NotificationService.Domain.Aggregates;

namespace NotificationService.Application.Abstractions;

public interface IIntegrationRepository
{
    Task<Integration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Integration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Integration>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Integration integration, CancellationToken cancellationToken = default);
    void Update(Integration integration);
    void Remove(Integration integration);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
