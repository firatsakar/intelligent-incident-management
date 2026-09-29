using IncidentService.Application.Abstractions;
using IncidentService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IncidentService.Infrastructure.Persistence.Repositories;

public sealed class IncidentApiKeyRepository : IIncidentApiKeyRepository
{
    private readonly IncidentDbContext _context;

    public IncidentApiKeyRepository(IncidentDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<IncidentApiKey>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.IncidentApiKeys.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task<IncidentApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.IncidentApiKeys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default) =>
        _context.IncidentApiKeys.AnyAsync(x => x.Name == name, cancellationToken);

    public Task<IncidentApiKey?> FindByHashForIntakeAsync(string keyHash, CancellationToken cancellationToken = default) =>
        _context.IncidentApiKeys.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.KeyHash == keyHash, cancellationToken);

    public async Task AddAsync(IncidentApiKey key, CancellationToken cancellationToken = default) =>
        await _context.IncidentApiKeys.AddAsync(key, cancellationToken);

    public void Remove(IncidentApiKey key) => _context.IncidentApiKeys.Remove(key);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
