using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Repositories;

public sealed class ErrorSignatureRepository : IErrorSignatureRepository
{
    private readonly TelemetryDbContext _context;

    public ErrorSignatureRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task<ErrorSignature?> GetByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.ErrorSignatures.FirstOrDefaultAsync(
            x => x.Fingerprint == fingerprint,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<ErrorSignature>> GetByFingerprintsAsync(
        IReadOnlyCollection<string> fingerprints,
        CancellationToken cancellationToken = default
    )
    {
        if (fingerprints.Count == 0)
            return [];

        // Tracked on purpose: the caller mutates what comes back.
        return await _context
            .ErrorSignatures.Where(x => fingerprints.Contains(x.Fingerprint))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ErrorSignature>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default
    )
    {
        if (ids.Count == 0)
            return [];

        return await _context
            .ErrorSignatures.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ErrorSignature signature,
        CancellationToken cancellationToken = default
    )
    {
        await _context.ErrorSignatures.AddAsync(signature, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
