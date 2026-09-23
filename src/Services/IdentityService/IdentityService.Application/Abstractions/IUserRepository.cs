using IdentityService.Domain.Aggregates;

namespace IdentityService.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up by the address as <see cref="User.Normalize"/> leaves it. Callers pass what the
    /// person typed; normalising here rather than at each call site is what keeps two spellings of
    /// one address from being two accounts.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
