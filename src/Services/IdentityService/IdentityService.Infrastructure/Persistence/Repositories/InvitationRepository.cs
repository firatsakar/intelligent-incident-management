using IdentityService.Application.Abstractions;
using IdentityService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Repositories;

// The identity tables carry no query filter — signing in happens before anyone's organisation is
// known — so every organisation-scoped read here names the organisation in its own predicate.
public sealed class InvitationRepository : IInvitationRepository
{
    private readonly IdentityDbContext _context;

    public InvitationRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Invitation?> GetAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Invitations.FirstOrDefaultAsync(
            x => x.Id == id && x.OrganizationId == organizationId,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<Invitation>> ListPendingAsync(
        Guid organizationId,
        DateTime asOf,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Invitations.AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId
                && x.AcceptedAt == null
                && x.RevokedAt == null
                && x.ExpiresAt > asOf
            )
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> ListPendingForEmailAsync(
        Guid organizationId,
        string email,
        DateTime asOf,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = User.Normalize(email);

        return await _context
            .Invitations.Where(x =>
                x.OrganizationId == organizationId
                && x.Email == normalized
                && x.AcceptedAt == null
                && x.RevokedAt == null
                && x.ExpiresAt > asOf
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<Invitation?> FindByTokenHashForAcceptAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Invitations.FirstOrDefaultAsync(
            x => x.TokenHash == tokenHash,
            cancellationToken
        );
    }

    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        await _context.Invitations.AddAsync(invitation, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
