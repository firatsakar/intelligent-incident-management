using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace AgentOrchestrator.Infrastructure.Persistence.Repositories;

public sealed class AiSettingsRepository : IAiSettingsRepository
{
    private readonly AgentDbContext _context;

    public AiSettingsRepository(AgentDbContext context)
    {
        _context = context;
    }

    // Through the organisation filter, which is what makes "the" settings the caller's own.
    public Task<AiSettings?> GetAsync(CancellationToken cancellationToken = default) =>
        _context.AiSettings.FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(AiSettings settings, CancellationToken cancellationToken = default) =>
        await _context.AiSettings.AddAsync(settings, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
