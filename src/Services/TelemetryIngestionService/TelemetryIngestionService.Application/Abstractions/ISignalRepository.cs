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

    // Newest first, capped, with the true count before the cap. The uncapped predecessor was the
    // one read in this service whose cost grew with the customer's error rate rather than with
    // anything the caller asked for.
    Task<(IReadOnlyList<Signal> Items, int TotalCount)> GetRecentAsync(
        DateTime from,
        DateTime to,
        int limit,
        int offset,
        CancellationToken cancellationToken = default
    );

    // Counts only, for the funnel and the per-service rollup. Never returns a Signal.
    Task<IReadOnlyList<SignalStatsRow>> GetWindowStatsRowsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Signal signal, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
