using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Abstractions;

public interface IDetectionRuleRepository
{
    Task<IReadOnlyList<DetectionRule>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task AddAsync(DetectionRule rule, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
