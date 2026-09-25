using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Abstractions;

public interface IErrorSignatureRepository
{
    Task<ErrorSignature?> GetByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ErrorSignature>> GetByFingerprintsAsync(
        IReadOnlyCollection<string> fingerprints,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ErrorSignature>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default
    );

    /// <summary>The signature attached to this incident, if any — tracked, for detaching.</summary>
    Task<ErrorSignature?> GetByCurrentIncidentAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(ErrorSignature signature, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
