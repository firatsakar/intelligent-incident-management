using IdentityService.Domain.Aggregates;

namespace IdentityService.Application.Abstractions;

public interface IInvitationRepository
{
    /// <summary>One invitation of this organisation; another organisation's answers null, as if it did not exist.</summary>
    Task<Invitation?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Invitation>> ListPendingAsync(
        Guid organizationId,
        DateTime asOf,
        CancellationToken cancellationToken = default
    );

    /// <summary>Pending invitations of this organisation to one address — the ones a new invitation replaces.</summary>
    Task<IReadOnlyList<Invitation>> ListPendingForEmailAsync(
        Guid organizationId,
        string email,
        DateTime asOf,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// The invitation a link's token belongs to, whichever organisation that is.
    /// </summary>
    /// <remarks>
    /// Crosses organisations and is named for it, like the telemetry service's reads for the poller
    /// and the OTLP endpoint: whoever opens the link has no account and therefore no organisation,
    /// so the row found here is what decides which organisation everything after belongs to.
    /// </remarks>
    Task<Invitation?> FindByTokenHashForAcceptAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
