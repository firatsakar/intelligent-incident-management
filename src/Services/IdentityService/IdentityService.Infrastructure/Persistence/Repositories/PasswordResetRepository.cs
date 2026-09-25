using IdentityService.Application.Abstractions;
using IdentityService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetRepository : IPasswordResetRepository
{
    private readonly IdentityDbContext _context;

    public PasswordResetRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PasswordReset>> ListUsableForUserAsync(
        Guid userId,
        DateTime asOf,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .PasswordResets.Where(x =>
                x.UserId == userId && x.UsedAt == null && x.RevokedAt == null && x.ExpiresAt > asOf
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<PasswordReset?> FindByTokenHashForResetAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.PasswordResets.FirstOrDefaultAsync(
            x => x.TokenHash == tokenHash,
            cancellationToken
        );
    }

    public async Task AddAsync(PasswordReset reset, CancellationToken cancellationToken = default)
    {
        await _context.PasswordResets.AddAsync(reset, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
