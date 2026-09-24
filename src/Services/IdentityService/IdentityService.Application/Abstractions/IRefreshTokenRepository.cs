using IdentityService.Domain.Aggregates;

namespace IdentityService.Application.Abstractions;

public interface IRefreshTokenRepository
{
    /// <summary>
    /// Finds the row for a presented token, spent or not. A revoked row still has to come back:
    /// its being revoked is exactly what reuse detection needs to see.
    /// </summary>
    Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Every token this user could still present, for the case where one of them turns up twice
    /// and the honest response is to end all of them. Rows rather than a bulk update, so the
    /// revocation goes through the aggregate and keeps its own rules.
    /// </summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(
        Guid userId,
        DateTime asOf,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops rows that expired before <paramref name="cutoff"/>. Rotation writes a new row on
    /// every refresh, so without this the table grows for as long as anyone keeps using the
    /// product — and a revoked row past its expiry proves nothing that its expiry does not.
    /// </summary>
    Task<int> DeleteExpiredBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
