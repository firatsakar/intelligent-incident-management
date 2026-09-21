using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Abstractions;

public interface ISignalRepository
{
    // A signature that keeps firing must not raise a fresh signal on every poll. This is what
    // tells the detector whether the current burst has already been reported.
    Task<Signal?> GetLatestForSignatureAsync(
        Guid errorSignatureId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Signal>> GetByStatusAsync(
        SignalStatus status,
        int limit,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Signal>> GetRecentAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Signal signal, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
