using IdentityService.Domain.Aggregates;

namespace IdentityService.Application.Abstractions;

public interface IPasswordResetRepository
{
    /// <summary>Links still usable for one person — the ones a newly issued link replaces.</summary>
    Task<IReadOnlyList<PasswordReset>> ListUsableForUserAsync(
        Guid userId,
        DateTime asOf,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// The reset a link's token belongs to. Crosses organisations for the same reason
    /// <see cref="IInvitationRepository.FindByTokenHashForAcceptAsync"/> does: whoever opens it is
    /// not signed in.
    /// </summary>
    Task<PasswordReset?> FindByTokenHashForResetAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(PasswordReset reset, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
