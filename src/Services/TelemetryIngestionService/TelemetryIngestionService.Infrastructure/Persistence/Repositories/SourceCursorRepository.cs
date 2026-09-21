using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Repositories;

public sealed class SourceCursorRepository : ISourceCursorRepository
{
    private readonly TelemetryDbContext _context;

    public SourceCursorRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task<SourceCursor> GetOrCreateAsync(
        Guid telemetrySourceId,
        CancellationToken cancellationToken = default
    )
    {
        var cursor = await _context.SourceCursors.FirstOrDefaultAsync(
            x => x.TelemetrySourceId == telemetrySourceId,
            cancellationToken
        );

        if (cursor is not null)
            return cursor;

        cursor = SourceCursor.Start(telemetrySourceId);

        await _context.SourceCursors.AddAsync(cursor, cancellationToken);

        return cursor;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
