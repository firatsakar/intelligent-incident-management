using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace AgentOrchestrator.Infrastructure.Persistence.Repositories;

public sealed class IncidentAnalysisRepository : IIncidentAnalysisRepository
{
    private readonly AgentDbContext _context;

    public IncidentAnalysisRepository(AgentDbContext context)
    {
        _context = context;
    }

    public async Task<IncidentAnalysis?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Analyses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IncidentAnalysis?> GetByIncidentIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Analyses.FirstOrDefaultAsync(
            x => x.IncidentId == incidentId,
            cancellationToken
        );
    }

    public async Task AddAsync(
        IncidentAnalysis analysis,
        CancellationToken cancellationToken = default
    )
    {
        await _context.Analyses.AddAsync(analysis, cancellationToken);
    }

    public void Update(IncidentAnalysis analysis)
    {
        _context.Analyses.Update(analysis);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
