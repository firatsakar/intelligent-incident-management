using IdentityService.Application.Abstractions;
using IdentityService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _context;

    public RefreshTokenRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.RefreshTokens.FirstOrDefaultAsync(
            x => x.TokenHash == tokenHash,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(
        Guid userId,
        DateTime asOf,
        CancellationToken cancellationToken = default
    )
    {
        // Tracked, not AsNoTracking: the caller revokes what comes back.
        return await _context
            .RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > asOf)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.AddAsync(token, cancellationToken);
    }

    public async Task<int> DeleteExpiredBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .RefreshTokens.Where(x => x.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
