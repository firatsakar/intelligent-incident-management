using IdentityService.Application.Abstractions;
using IdentityService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly IdentityDbContext _context;

    public OrganizationRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Organizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Organizations.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(
        Organization organization,
        CancellationToken cancellationToken = default
    )
    {
        await _context.Organizations.AddAsync(organization, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
